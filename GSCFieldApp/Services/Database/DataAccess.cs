﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GSCFieldApp.Models;
using static GSCFieldApp.Dictionaries.DatabaseLiterals;
using GSCFieldApp.Dictionaries;
using SQLite;
using System.Diagnostics;
using BruTile.Wmts.Generated;
using GSCFieldApp.Controls;
using NetTopologySuite.Index.HPRtree;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System.Globalization;

namespace GSCFieldApp.Services.DatabaseServices
{
    public class DataAccess
    {
        public static SQLiteAsyncConnection _dbConnection;

        //Localization
        public LocalizationResourceManager LocalizationResourceManager
            => LocalizationResourceManager.Instance; // Will be used for in code dynamic local strings

        //TODO: find why on android .gpkg isn't a valid file type even though the database is sqlite.

#if WINDOWS
        public const string DatabaseFilename = DatabaseLiterals.DBName + DatabaseLiterals.DBTypeSqlite;
#elif ANDROID
        public const string DatabaseFilename = DatabaseLiterals.DBName + DatabaseLiterals.DBTypeSqliteDeprecated;
#else
        public const string DatabaseFilename = DatabaseLiterals.DBName + DatabaseLiterals.DBTypeSqlite;
#endif
        /// <summary>
        /// Default database patch in the app directory.
        /// Will be saved as another name once the field book is properly filled and then created.
        /// </summary>
        public string DatabaseFilePath =>
            Path.Combine(FileSystem.Current.AppDataDirectory, DatabaseFilename);

        /// <summary>
        /// Property directly set within user preferences
        /// </summary>
        public string PreferedDatabasePath
        {
            get { return Preferences.Get(nameof(DatabaseFilePath), DatabaseFilePath); }
            set { Preferences.Set(nameof(DatabaseFilePath), value); }
        }

        public DataAccess()
        {

        }

        #region DB MANAGEMENT METHODS

        /// <summary>
        /// Get a sqlite connection object
        /// </summary>
        private SQLiteAsyncConnection DbConnection
        {
            get
            {
                if (PreferedDatabasePath != string.Empty)
                {
                    return new SQLiteAsyncConnection(PreferedDatabasePath);
                }
                else
                {
                    return _dbConnection;
                }

            }
        }

        public SQLiteAsyncConnection GetConnectionFromPath(string inPath)
        {
            return new SQLiteAsyncConnection(inPath);
        }

        /// <summary>
        /// Will close the database connection
        /// </summary>
        /// <returns></returns>
        public async Task CloseConnectionAsync()
        {
            await DbConnection.CloseAsync();
        }

        #endregion

        #region DATA MANAGEMENT METHODS (Create, Update, Read)

        /// <summary>
        /// Will write an embedded resource to a file with a binary writer. In case it exists, it will replace it.
        /// Will save the resource to the local folder.
        /// </summary>
        public async Task<bool> CreateDatabaseFromResource(string outputDatabasePath, string resourceName = "")
        {
            try
            {
                if (!File.Exists(outputDatabasePath))
                {
                    if (resourceName == string.Empty)
                    {
                        resourceName = @"GSCFieldwork.gpkg";
                    }

                    //Open stream with embeded resource
                    using Stream package = await FileSystem.OpenAppPackageFileAsync(resourceName);

                    //Open empty stream for output file
                    using FileStream outputStream = System.IO.File.OpenWrite(outputDatabasePath);

                    //Need a binary write for geopackage database, else file will be corrupt with 
                    //default stream writer/reader
                    byte[] buffer = new byte[16*1024];
                    using (BinaryWriter fileWriter = new BinaryWriter(outputStream))
                    {
                        using (BinaryReader fileReader = new BinaryReader(package))
                        {
                            //NOTE: On android length isn't a property so we need to count the bytes instead
                            //https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers?view=net-maui-7.0&tabs=android#platform-differences 

                            //Read package by block of 1024 bytes.
                            int readCount = 0;

                            while ((readCount = fileReader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fileWriter.Write(buffer, 0, readCount);
                            }

                        }
                    }

                    //Keep database path as default
                    PreferedDatabasePath = outputDatabasePath;
                }

                return true;
                
            }
            catch (Exception e)
            {

                new ErrorToLogFile(e).WriteToFile();
                await Shell.Current.DisplayAlert("Error", e.Message, "Ok");
                return false;
            }


        }

        /// <summary>
        /// Will delete an item object that is a model table
        /// </summary>
        /// <param name="item"></param>
        /// <param name="doUpdate"></param>
        /// <returns></returns>
        public async Task<int> DeleteItemAsync(object item)
        {
            if (item != null)
            {
                // Create a new connection
                try
                {

                    //For debug
                    DbConnection.Tracer = new Action<string>(q => Debug.WriteLine(q));
                    DbConnection.Trace = true;


                    int numberOfRows = await DbConnection.DeleteAsync(item);
                    return numberOfRows;



                }
                catch (SQLite.SQLiteException ex)
                {
                    new ErrorToLogFile(ex).WriteToFile();
                    return 0;
                }
            }
            else
            {
                return 0;
            }

        }

        /// <summary>
        /// Will save (insert or update) an item object that is a model table
        /// </summary>
        /// <param name="item"></param>
        /// <param name="doUpdate"></param>
        /// <returns></returns>
        public async Task<object> SaveItemAsync(object item, bool doUpdate)
        {

            // Create a new connection
            try
            {

                //For debug
                DbConnection.Tracer = new Action<string>(q => Debug.WriteLine(q));
                DbConnection.Trace = true;

                if (doUpdate)
                {
                    await DbConnection.UpdateAsync(item);
                    return item;

                }
                else
                {
                    await DbConnection.InsertAsync(item);
                    return item;
                }

            }
            catch (SQLite.SQLiteException ex)
            {
                new ErrorToLogFile(ex).WriteToFile();
                return item;
            }

        }

        /// <summary>
        /// Will return a list containing value to fill comboboxes related to the database model
        /// </summary>
        /// <param name="tableName">The table name to use with with the picklist</param>
        /// <param name="fieldName">The database table field to get vocabs from</param>
        /// <param name="allValues">If all values, even non visible vocabs are needed</param>
        /// <param name="extraFieldValue"> The parent field that will be used to filter vocabs</param>
        /// <param name="fieldwork">Field book theme (bedrock, surficial)</param>
        /// <returns>A list contain resulting voca class entries</returns>
        public async Task<List<Vocabularies>> GetPicklistValuesAsync(string tableName, string fieldName, string extraFieldValue, 
            bool allValues)
        {

            //Get the current project type
            string fieldworkType = ApplicationThemeBedrock; //Default

            if (Preferences.ContainsKey(nameof(FieldUserInfoFWorkType)))
            {
                //This should be set whenever user selects a different field book
                fieldworkType = Preferences.Get(nameof(FieldUserInfoFWorkType), fieldworkType);
            }

            //Build query
            string querySelect = "SELECT md.* FROM " + TableDictionary + " as md";
            string queryJoin = " JOIN " + TableDictionaryManager + " as mdm ON md." + FieldDictionaryCodedTheme + " = mdm." + FieldDictionaryManagerCodedTheme;
            string queryWhere = " WHERE mdm." + FieldDictionaryManagerAssignTable + " = '" + tableName + "'";
            string queryAndField = " AND mdm." + FieldDictionaryManagerAssignField + " = '" + fieldName + "'";
            string queryAndVisible = " AND md." + FieldDictionaryVisible + " = '" + boolYes + "'";
            string queryAndWorkType = string.Empty;
            string queryAndParent = string.Empty;
            string queryOrdering = " ORDER BY md." + FieldDictionaryOrder + " ASC";

            if (fieldworkType != string.Empty)
            {
                queryAndWorkType = " AND (lower(mdm." + FieldDictionaryManagerSpecificTo + ") like '" + fieldworkType + "%' OR lower(mdm." + FieldDictionaryManagerSpecificTo + ") = '')";
            }

            if (extraFieldValue != string.Empty && extraFieldValue != null && extraFieldValue != "")
            {
                queryAndParent = " AND md." + FieldDictionaryRelatedTo + " = '" + extraFieldValue + "'";
            }

            string finalQuery = querySelect + queryJoin + queryWhere + queryAndField + queryAndWorkType + queryAndParent;
            if (!allValues)
            {
                finalQuery = finalQuery + queryAndVisible + queryOrdering;
            }
            else
            {
                finalQuery = finalQuery + queryOrdering;
            }

            //Get vocab records
            SQLiteAsyncConnection currentConnection = GetConnectionFromPath(PreferedDatabasePath);

            List<Vocabularies> vocabs = await currentConnection.QueryAsync<Vocabularies>(finalQuery);

            //Add empty record for user to remove any selected values
            Vocabularies emptyNull = new Vocabularies();
            emptyNull.Code = null;
            emptyNull.Description = " ";

            vocabs.Insert(0, emptyNull);

            return vocabs;
        }

        /// <summary>
        /// From a given table and field name, will retrieve associated vocabulary and
        /// output a list of combobox items. An output parameter is also available 
        /// for default value if one is stated in the database or if N.A. is the only available choice.
        /// This method is meant for generic list with no queries
        /// </summary>
        /// <param name="tableName">The table name associated with the wanted vocab.</param>
        /// <param name="fieldName">The field name associated with the wanted vocab.</param>
        /// <returns></returns>
        public async Task<ComboBox> GetComboboxListWithVocabAsync(string tableName, string fieldName, string extraFieldValue = "")
        {
            //Outputs
            ComboBox outputVocabs = new ComboBox();

            //Get vocab
            List<Vocabularies> vocs = await GetPicklistValuesAsync(tableName, fieldName, extraFieldValue, false);

            //Fill in cbox
            outputVocabs = GetComboboxListFromVocab(vocs);


            return outputVocabs;
        }

        /// <summary>
        /// From a given list of vocabularies items (usually coming from a more define query), will
        /// output a list of combobox items. Will also output as in the default value else -1 for no
        /// selection
        /// </summary>
        /// <param name="inVocab">List of vocabularies that needs to be converted to picker</param>
        /// <returns></returns>
        public ComboBox GetComboboxListFromVocab(IEnumerable<Vocabularies> inVocab, bool SymbolAsValue = false)
        {
            //Outputs
            List<ComboBoxItem> outputVocabsList = new List<ComboBoxItem>();
            int defaultValueIndex = -1;

            //Fill in cbox
            foreach (Vocabularies vocabs in inVocab)
            {
                ComboBoxItem newItem = new ComboBoxItem();

                //Manage nulls
                if (vocabs.Code == null)
                {
                    newItem.itemValue = string.Empty;
                }
                else
                {
                    newItem.itemValue = vocabs.Code;
                }

                //Manage symbols over code
                if (vocabs.Symbol != null && SymbolAsValue)
                {
                    newItem.itemValue = vocabs.Symbol;
                }

                //Manage description null
                if (vocabs.Description == null)
                {
                    newItem.itemName = string.Empty;
                }
                else
                {
                    newItem.itemName = vocabs.Description;
                }

                //Manage language description
                if (CultureInfo.CurrentCulture.ToString().ToLower().Contains("fr"))
                {

                    if (vocabs.DescriptionFR != null && vocabs.DescriptionFR != string.Empty)
                    {
                        newItem.itemName = vocabs.DescriptionFR;
                    }
                    else
                    {
                        newItem.itemName = vocabs.Description;
                    }

                }
                else
                {
                    newItem.itemName = vocabs.Description;
                }

                //Select default if stated in database
                if (vocabs.DefaultValue != null && vocabs.DefaultValue == Dictionaries.DatabaseLiterals.boolYes)
                {
                    defaultValueIndex = outputVocabsList.Count;
                }

                //Keep relatedTo Value for filtering
                if (vocabs.RelatedTo != null && vocabs.RelatedTo != string.Empty)
                {
                    newItem.itemParent = vocabs.RelatedTo;
                }

                outputVocabsList.Add(newItem);
            }

            //If at the end there is onlye one record, make it a default
            if (outputVocabsList.Count == 1)
            {
                defaultValueIndex = 0;
            }

            //Set
            ComboBox outputVocabs = new ComboBox();
            outputVocabs.cboxItems = outputVocabsList;
            outputVocabs.cboxDefaultItemIndex = defaultValueIndex; 

            return outputVocabs;
        }

        /// <summary>
        /// From a given list of vocabularies items (usually coming from a more define query), will
        /// output a list of combobox items. Will also output as in the default value else -1 for no
        /// selection
        /// </summary>
        /// <param name="inVocab">List of vocabularies that needs to be converted to picker</param>
        /// <returns></returns>
        public ComboBox GetComboboxListFromStrings(IEnumerable<string> inStrings)
        {
            //Outputs
            List<ComboBoxItem> outputStringList = new List<ComboBoxItem>();
            int defaultValueIndex = -1;

            //Fill in cbox
            foreach (string s in inStrings)
            {
                ComboBoxItem newItem = new ComboBoxItem();

                newItem.itemValue = s;
                newItem.itemName = s;

                outputStringList.Add(newItem);
            }

            //If at the end there is onlye one record, make it a default
            if (outputStringList.Count == 1)
            {
                defaultValueIndex = 0;
            }

            //Set
            ComboBox outputCombo = new ComboBox();
            outputCombo.cboxItems = outputStringList;
            outputCombo.cboxDefaultItemIndex = defaultValueIndex;

            return outputCombo;
        }

        /// <summary>
        /// Will delete any record from given parameters.
        /// TODO: The field name entry could be replace with the prime key if a TableMapping object is created. I think it returns the prime key field name. - Gab
        /// </summary>
        /// <param name="tableName">The table name to delete the record from</param>
        /// <param name="tableFieldName">The table field name to select the record with</param>
        /// <param name="recordIDToDelete">The table field value to delete.</param>
        public async Task<int> DeleteItemCascadeAsync(string tableName, string tableFieldName, int recordIDToDelete)
        {

            // Create a new connection
            try
            {

                //For debug
                DbConnection.Tracer = new Action<string>(q => Debug.WriteLine(q));
                DbConnection.Trace = true;


                await DbConnection.ExecuteAsync("PRAGMA foreign_keys=ON");
                int delRecords = await DbConnection.ExecuteAsync(string.Format("DELETE FROM {0} WHERE {1} = {2};", tableName, tableFieldName, recordIDToDelete));
                return delRecords;



            }
            catch (SQLite.SQLiteException ex)
            {
                new ErrorToLogFile(ex).WriteToFile();
                return 0;
            }


        }

        #endregion

        #region GET METHODS (usually needs a connection object)
        /// <summary>
        /// Will return a table mapping object create from a type object that represent the table to map.
        /// </summary>
        /// <param name="tableName">The table type object from boxing a class into a type</param>
        /// <param name="dbConnect">An existing database connection</param>
        /// <returns>Will return an empty list if table name wasn't found in database.</returns>
        private static async Task<TableMapping> GetATableObjectAsync(Type tableType, SQLiteAsyncConnection dbConnect)
        {
            //Will return a TableMapping object created from the given type. Type, deriving from the model class, should be true, else 
            //things might fail.
            return await dbConnect.GetMappingAsync(tableType);
        }

        /// <summary>
        /// Will return the number of records of a table
        /// </summary>
        /// <param name="inTabelType"></param>
        /// <returns></returns>
        public async Task<int> GetTableCount(Type inTableType)
        {
            //Variables
            int tableCount = 0;

            //Get query result
            SQLiteAsyncConnection dbConnect = GetConnectionFromPath(PreferedDatabasePath);
            TableMapping tableMapping = await GetATableObjectAsync(inTableType, dbConnect);

            List<int> tableRows = await dbConnect.QueryScalarsAsync<int>("SELECT * FROM " + tableMapping.TableName);

            if (tableRows.Count() > 0)
            {
                tableCount = tableRows.Count();
            }
            

            await dbConnect.CloseAsync();
            
            return tableCount;

        }

        /// <summary>
        /// Will return a related structure record as an object
        /// </summary>
        /// <param name="StrucId"></param>
        /// <returns></returns>
        public async Task<Structure> GetRelatedStructure(int? StrucId)
        {
            Structure relatedStructure = new Structure();
            if (StrucId != null)
            {
                SQLiteAsyncConnection dbConnect = GetConnectionFromPath(PreferedDatabasePath);
                List<Structure> relatedStructures = await dbConnect.Table<Structure>().Where(struc => struc.StructureID == StrucId).ToListAsync();

                if (relatedStructures != null  && relatedStructures.Count > 0)
                {
                    relatedStructure = relatedStructures[0];
                }

                await dbConnect.CloseAsync();
                
            }

            return relatedStructure;
        }

        /// <summary>
        /// Will get a read from F_METADATA.VERSIONSCHEMA and will return value in double
        /// </summary>
        /// <returns></returns>
        public async Task<double> GetDBVersion()
        {
            double schemaVersion = 0.0;

            SQLiteAsyncConnection dbConnect = GetConnectionFromPath(PreferedDatabasePath);
            List<Metadata> mets = await dbConnect.Table<Metadata>().ToListAsync();
            await dbConnect.CloseAsync();

            if (mets != null && mets.Count() > 0)
            {
                //Default to first one 

                //Parse result
                if (mets[0].VersionSchema != null)
                {
                    Double.TryParse(mets[0].VersionSchema.ToString(), out schemaVersion);
                }

            }

            return schemaVersion;
        }

        /// <summary>
        /// Will take an input database and will upgrade output database vocab tables (dictionaries) with latest coming from an input version
        /// </summary>
        public async Task<bool> GetLatestVocab(string vocabFromDBPath, SQLiteAsyncConnection vocabToDBConnection, double fromDBVersion, bool closeConnection = true)
        {
            //Output
            bool completedWithoutErrors = false;

            //Will hold all queries needed to be committed
            List<string> queryList = new List<string>() { };
            List<Exception> exceptionList = new List<Exception>();

            //Build attach db query
            string attachDBName = "db2";
            string attachQuery = "ATTACH '" + vocabFromDBPath + "' AS " + attachDBName + ";";
            queryList.Add(attachQuery);

            //Build insert queries
            #region M_DICTIONARY

            Vocabularies modelVocab = new Vocabularies();
            List<string> vocabFieldList = modelVocab.getFieldList[DBVersion];
            string vocab_querySelect = string.Empty;

            foreach (string vocabFields in vocabFieldList)
            {
                //Get all fields except alias
                if (vocabFields != vocabFieldList.First())
                {

                    if (vocabFields == DatabaseLiterals.FieldDictionaryVersion && fromDBVersion >= 1.5)
                    {
                        vocab_querySelect = vocab_querySelect +
                            ", iif(NOT EXISTS (SELECT sql from " + attachDBName + ".sqlite_master where sql LIKE '%" + DatabaseLiterals.TableDictionary + "%" + DatabaseLiterals.FieldDictionaryVersion +
                            "%'),v." + DatabaseLiterals.FieldDictionaryVersion + ",NULL) as " + DatabaseLiterals.FieldDictionaryVersion;
                    }
                    else if (vocabFields == DatabaseLiterals.FieldDictionaryVersion && fromDBVersion == 1.44)
                    {
                        vocab_querySelect = vocab_querySelect +
                            ", NULL as " + DatabaseLiterals.FieldDictionaryVersion;
                    }
                    else if (vocabFields == DatabaseLiterals.FieldDictionaryVersion && fromDBVersion < 1.5)
                    {
                        //Do nothing, field didn't exist
                    }
                    else
                    {
                        vocab_querySelect = vocab_querySelect + ", v." + vocabFields + " as " + vocabFields;
                    }

                }
                else
                {
                    if (vocabFields == FieldGenericRowID && fromDBVersion >= DBVersion160)
                    {
                        vocab_querySelect = " NULL as " + vocabFields; //Don't insert the ids back
                    }
                    else if (vocabFields == FieldGenericRowID && fromDBVersion < DBVersion160)
                    {
                        //Do nothing, skip that one, it was added in version 1.7
                    }


                }

            }
            vocab_querySelect = vocab_querySelect.Replace(", ,", "");

            string insertQuery_vocab = "INSERT INTO " + DatabaseLiterals.TableDictionary + " SELECT " + vocab_querySelect;
            insertQuery_vocab = insertQuery_vocab.Replace("SELECT ,", "SELECT ");
            insertQuery_vocab = insertQuery_vocab + " FROM " + attachDBName + "." + DatabaseLiterals.TableDictionary + " as v";

            insertQuery_vocab = insertQuery_vocab + " WHERE v." + FieldDictionaryCreator + " not in (select distinct(md." +
                FieldDictionaryCreator + ") from " + TableDictionary + " as md)";
            insertQuery_vocab = insertQuery_vocab + " AND v." + FieldDictionaryTermID + " not in (select md." + FieldDictionaryTermID + " from " +
                TableDictionary + " as md);";
            queryList.Add(insertQuery_vocab);

            #endregion

            //Build detach query
            string detachQuery = "DETACH DATABASE " + attachDBName + ";";
            queryList.Add(detachQuery);

            //Build vacuum query
            string vacuumQuery = "VACUUM;";
            queryList.Add(vacuumQuery);

            //Update working database
            foreach (string q in queryList)
            {
                try
                {
                    await vocabToDBConnection.ExecuteAsync(q);
                }
                catch (Exception e)
                {
                    exceptionList.Add(e);
                }

            }

            //Close if needed
            if (closeConnection)
            {

                await vocabToDBConnection.CloseAsync();
            }

            //Process exceptions
            if (exceptionList.Count > 0)
            {
                string wholeStack = string.Empty;

                foreach (Exception es in exceptionList)
                {
                    wholeStack = wholeStack + "; " + es.Message + "; " + es.StackTrace;
                }

                foreach (string q in queryList)
                {
                    wholeStack = wholeStack + "\n " + q;
                }

                //Log
                new ErrorToLogFile(wholeStack + "\n DBVersion:" + fromDBVersion).WriteToFile();

            }
            else
            {
                completedWithoutErrors = true;
            }

            return completedWithoutErrors;
        }


        #endregion

    }
}
