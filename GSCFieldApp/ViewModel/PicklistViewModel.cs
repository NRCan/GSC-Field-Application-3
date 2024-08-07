﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GSCFieldApp.Models;
using GSCFieldApp.Services.DatabaseServices;
using GSCFieldApp.Controls;
using GSCFieldApp.Views;
using GSCFieldApp.Services;
using static GSCFieldApp.Dictionaries.DatabaseLiterals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using CommunityToolkit.Maui.Alerts;
using System.IO;
using System.Collections.ObjectModel;

namespace GSCFieldApp.ViewModel
{
    public partial class PicklistViewModel : FieldAppPageHelper
    {
        #region INIT
        private Vocabularies _model = new Vocabularies();
        private Picklist _modelPicklist = new Picklist();
        private ComboBox _picklistTables = new ComboBox();
        private ComboBox _picklistFields = new ComboBox();
        private ComboBox _picklistParents = new ComboBox();
        private List<VocabularyManager> _vocabularyManagers = new List<VocabularyManager>();
        private ObservableCollection<Vocabularies> _picklistValues = new ObservableCollection<Vocabularies>();

        #endregion

        #region RELAYS
        /// <summary>
        /// Will add a new term within the database
        /// </summary>
        [RelayCommand]
        async void AddNewTerm()
        {

            string popUpTitle = LocalizationResourceManager["PicklistPageAddNewTermTitle"].ToString();
            string popUpContent = LocalizationResourceManager["PicklistPageAddNewTermContent"].ToString();
            string result = await Shell.Current.DisplayPromptAsync(popUpTitle, popUpContent);

            if (result != null && result != string.Empty)
            {
                //Trim
                result = result.Trim();

                //Set
                Vocabularies newVocab = new Vocabularies();
                newVocab.Code = result;
                newVocab.Description = result;
                newVocab.DescriptionFR = result;
                newVocab.TermID = idCalculator.CalculateGUID();
                newVocab.CodedTheme = _picklistValues.First().CodedTheme; //Steal from first item
                newVocab.Visibility = boolYes;
                newVocab.Editable = boolYes;
                newVocab.Order = 0.0; //Make sure to add in first place
                newVocab.Creator = Preferences.Get(nameof(FieldUserInfoUCode), AppInfo.Current.Name);
                newVocab.CreatorDate = String.Format("{0:yyyy-MM-dd}", DateTime.Now);

                //Add
                _picklistValues.Insert(0, newVocab);
                OnPropertyChanged(nameof(PicklistValues));
            }

        }

        /// <summary>
        /// Will mod a term
        /// </summary>
        [RelayCommand]
        async void ModifyTerm(Vocabularies vocabToEdit)
        {
            if (vocabToEdit != null)
            {
                string popUpTitle = LocalizationResourceManager["PicklistPageModifyTermTitle"].ToString();
                string popUpContent = LocalizationResourceManager["PicklistPageModifyTermContent"].ToString();
                string popUpButtonOK = LocalizationResourceManager["GenericButtonOk"].ToString();
                string popUpButtonCancel = LocalizationResourceManager["GenericButtonCancel"].ToString();
                string result = await Shell.Current.DisplayPromptAsync(popUpTitle, popUpContent, popUpButtonOK, popUpButtonCancel, null, -1, null, vocabToEdit.Description);

                if (result != null && result != string.Empty)
                {
                    //Trim
                    result = result.Trim();

                    //Set
                    vocabToEdit.Description = result;
                    vocabToEdit.DescriptionFR = result;
                    vocabToEdit.Editor = Preferences.Get(nameof(FieldUserInfoUCode), AppInfo.Current.Name);
                    vocabToEdit.EditorDate = String.Format("{0:yyyy-MM-dd}", DateTime.Now);

                    //Replace 
                    int vocabIndex = -1;
                    foreach (Vocabularies voc in _picklistValues)
                    {
                        if (voc.TermID == vocabToEdit.TermID)
                        {
                            vocabIndex = _picklistValues.IndexOf(voc);
                        }
                    }
                    _picklistValues.RemoveAt(vocabIndex);
                    _picklistValues.Insert(vocabIndex, vocabToEdit);
                    OnPropertyChanged(nameof(PicklistValues));
                }
            }


        }


        /// <summary>
        /// Will force a ascending alphabetical order sort on the picklist values
        /// </summary>
        [RelayCommand]
        async void SortTerm()
        {
            if (_picklistValues != null && _picklistValues.Count > 0)
            {
                List<Vocabularies> sortedVocab = _picklistValues.OrderBy(v => v.Description).ToList();
                _picklistValues.Clear();
                foreach (Vocabularies vs in sortedVocab)
                {
                    _picklistValues.Add(vs);
                }

                OnPropertyChanged(nameof(PicklistValues));
            }

        }

        [RelayCommand]
        async void SetDefaultTerm(Vocabularies vocabToEdit)
        {
            if (vocabToEdit != null)
            {
                //Get a new list so we can edit later one the real one
                List<Vocabularies> copiedVocbs = _picklistValues.ToList();
                _picklistValues.Clear();



                foreach (Vocabularies vocs in copiedVocbs)
                {
                    int currentIndex = copiedVocbs.IndexOf(vocs);
                    if (vocs.TermID == vocabToEdit.TermID)
                    {
                        //Set as default selected value or reverse
                        if (vocabToEdit.DefaultValue == boolYes)
                        {
                            vocabToEdit.DefaultValue = boolNo;
                        }
                        else
                        {
                            vocabToEdit.DefaultValue = boolYes;
                        }
                        
                        _picklistValues.Add(vocabToEdit);
                    }
                    else
                    {
                        //Unset all other values
                        vocs.DefaultValue = boolNo;
                        _picklistValues.Add(vocs);
                    }
                }
                OnPropertyChanged(nameof(PicklistValues));
            }


        }

        #endregion

        #region PROPERTIES

        public Vocabularies Model { get { return _model; } set { _model = value; } }
        public Picklist ModelPicklist { get { return _modelPicklist; } set { _modelPicklist = value; } }
        public ComboBox PicklistTables { get { return _picklistTables; } set { _picklistTables = value; } }
        public ComboBox PicklistFields { get { return _picklistFields; } set { _picklistFields = value; } }
        public ComboBox PicklistParents { get { return _picklistParents; } set { _picklistParents = value; } }
        public ObservableCollection<Vocabularies> PicklistValues { get { return _picklistValues; } set { _picklistValues = value; } }
        #endregion

        public PicklistViewModel()
        {
            _ = FillPickers();
        }

        #region METHODS

        /// <summary>
        /// Will fill all picker controls
        /// TODO: make sure this whole thing doesn't slow too much form rendering
        /// </summary>
        /// <returns></returns>
        public async Task FillPickers()
        {
            //Get the current project type
            string fieldworkType = ApplicationThemeBedrock; //Default

            if (Preferences.ContainsKey(nameof(FieldUserInfoFWorkType)))
            {
                //This should be set whenever user selects a different field book
                fieldworkType = Preferences.Get(nameof(FieldUserInfoFWorkType), fieldworkType);
            }

            //Connect
            SQLiteAsyncConnection currentConnection = da.GetConnectionFromPath(da.PreferedDatabasePath);
            _vocabularyManagers = await currentConnection.Table<VocabularyManager>().Where(e => e.ThemeEditable == boolYes && (e.ThemeProjectType == fieldworkType || e.ThemeProjectType == string.Empty)).ToListAsync();

            //Special fill for table names
            _picklistTables = await FillTablePicklist(currentConnection);
            OnPropertyChanged(nameof(PicklistTables));

            await currentConnection.CloseAsync();
        }

        /// <summary>
        /// Will go through the list of table names from M_DICTIONARY_MANAGER
        /// instead of the default vocab list from M_DICTIONNARY
        /// </summary>
        /// <returns></returns>
        private async Task<ComboBox> FillTablePicklist(SQLiteAsyncConnection inConnection)
        {
            //Build combobox object
            List<Vocabularies> vocTable = new List<Vocabularies>();
            List<string> parsedVoc = new List<string>();

            if (_vocabularyManagers != null && _vocabularyManagers.Count > 0)
            {

                foreach (VocabularyManager vcms in _vocabularyManagers)
                {
                    if (!parsedVoc.Contains(vcms.ThemeTable))
                    {
                        //Spoof a vocab object and get localized table name
                        Vocabularies voc = new Vocabularies();
                        voc.Code = vcms.ThemeTable;
                        voc.Description = vcms.ThemeTable;

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.environment.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesEnvironmentHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.document.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesPhotoHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.drill.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesDrillHolesHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(KeywordEarthmat))
                        {
                            voc.Code = vcms.ThemeTable;
                            voc.Description = LocalizationResourceManager["FielNotesEMHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.fossil.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesFossilHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.location.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesLocationHeader"].ToString();
                        }

                        if (vcms.ThemeTable.ToLower().Contains(TableNames.meta.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FieldBookPageTitle"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(TableNames.mineral.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesMineralHeader"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(TableNames.mineralization.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesMineralizationHeader"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(KeywordPflow))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesPaleoflowHeader"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(TableNames.sample.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesSampleHeader"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(TableNames.station.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesStationHeader"].ToString();
                        }
                        if (vcms.ThemeTable.ToLower().Contains(TableNames.structure.ToString()))
                        {
                            voc.Description = LocalizationResourceManager["FielNotesStructureHeader"].ToString();
                        }

                        //Prevent bs from beind added.
                        if (voc.Code != null && voc.Code != string.Empty)
                        {
                            vocTable.Add(voc);

                            parsedVoc.Add(vcms.ThemeTable);
                        }


                    }

                }

            }

            //Convert to custom picker
            ComboBox tableBox = da.GetComboboxListFromVocab(vocTable);

            //Sort based on localized value
            List<ComboBoxItem> sortedTableBox = tableBox.cboxItems.OrderBy(t => t.itemName).ToList();
            tableBox.cboxItems = sortedTableBox;

            return tableBox;

        }

        /// <summary>
        /// Will go through the list of field names from M_DICTIONARY_MANAGER
        /// instead of the default vocab list from M_DICTIONNARY
        /// </summary>
        /// <returns></returns>
        public void FillFieldsPicklist()
        {
            //Build combobox object
            List<Vocabularies> vocTable = new List<Vocabularies>();
            List<string> parsedVoc = new List<string>();

            if (_vocabularyManagers != null && _vocabularyManagers.Count > 0 && ModelPicklist.PicklistName != string.Empty)
            {
                List<VocabularyManager> subVocabList = _vocabularyManagers.Where(v => v.ThemeTable == ModelPicklist.PicklistName).ToList();
                foreach (VocabularyManager vcms in subVocabList)
                {
                    if (!parsedVoc.Contains(vcms.ThemeField))
                    {
                        //Spoof a vocab object and get localized table name
                        Vocabularies voc = new Vocabularies();
                        voc.Code = vcms.ThemeName;
                        voc.Description = vcms.ThemeNameDesc;

                        //Prevent bs from beind added.
                        if (voc.Code != null && voc.Code != string.Empty)
                        {
                            vocTable.Add(voc);
                            parsedVoc.Add(vcms.ThemeField);
                        }


                    }

                }

            }

            //Convert to custom picker
            ComboBox fieldBox = da.GetComboboxListFromVocab(vocTable);

            //Sort based on localized value
            List<ComboBoxItem> sortedFieldBox = fieldBox.cboxItems.OrderBy(t => t.itemName).ToList();
            fieldBox.cboxItems = sortedFieldBox;

            _picklistFields = fieldBox;
            OnPropertyChanged(nameof(PicklistFields));

        }

        /// <summary>
        /// Will fill out the list view of all picklist values
        /// based on selected table and field 
        /// </summary>
        public async void FillFieldValuesPicklist()
        {
            //Get the values
            List<Vocabularies> incomingValues = new List<Vocabularies>();

            if (_modelPicklist.PicklistParent != null && _modelPicklist.PicklistParent != string.Empty)
            {
                incomingValues = await da.GetPicklistValuesAsync(ModelPicklist.PicklistName, ModelPicklist.PicklistField, _modelPicklist.PicklistParent, false);
            }
            else
            {
                incomingValues = await da.GetPicklistValuesAsync(ModelPicklist.PicklistName, ModelPicklist.PicklistField, "", false);
            }

            if (incomingValues != null && incomingValues.Count > 0)
            {
                //Init
                _picklistValues.Clear();
                OnPropertyChanged(nameof(PicklistValues));

                //Convert for usage in xaml template
                foreach (Vocabularies v in incomingValues)
                {
                    _picklistValues.Add(v);
                }
            }

            OnPropertyChanged(nameof(PicklistValues));

        }

        /// <summary>
        /// Will fill out the picker of parent values based on user selected field.
        /// </summary>
        public async Task<bool> FillFieldParentValuesPicklist()
        {
            bool doesHaveParents = false;

            if (_modelPicklist.PicklistField != null && _modelPicklist.PicklistField != string.Empty)
            {
                //Build query to retrieve unique parents
                //select * from M_DICTIONARY m WHERE m.CODETHEME in 
                string querySelect_1 = "select * from " + TableDictionary + " m ";
                string queryWhere_1 = " WHERE m." + FieldDictionaryCodedTheme + " in ";

                //(select m.CODETHEME from M_DICTIONARY m join M_DICTIONARY_MANAGER mdm on m.CODETHEME = mdm.CODETHEME WHERE m.CODE in 
                string querySelect_2 = "(select m." + FieldDictionaryCodedTheme + " from " + TableDictionary + " m ";
                string querySelect_2_join = "join " + TableDictionaryManager + " mdm on m." + FieldDictionaryCodedTheme + " = mdm." + FieldDictionaryCodedTheme + " ";
                string queryWhere_2 = " WHERE m." + FieldDictionaryCode + " in ";

                //(select distinct(m.RELATEDTO) from M_DICTIONARY m WHERE m.CODETHEME = 'MODTEXTURE' ORDER BY m.RELATEDTO ) and mdm.ASSIGNTABLE in 
                string querySelect_3 = "(select distinct(m." + FieldDictionaryRelatedTo + ") from " + TableDictionary + " m ";
                string queryWhere_3 = " WHERE m." + FieldDictionaryCodedTheme + " = '" + _modelPicklist.PicklistField + "'";
                string queryOrderBy_3 = " ORDER BY m." + FieldDictionaryRelatedTo + " ) and mdm." + FieldDictionaryManagerAssignTable + " in ";

                //(select mdm2.ASSIGNTABLE from M_DICTIONARY_MANAGER mdm2 where mdm2.CODETHEME = 'MODTEXTURE'))  AND m.VISIBLE = 'Y' ORDER BY m.DESCRIPTIONEN ASC
                string queryWhere_1_2 = "(select mdm2." + FieldDictionaryManagerAssignTable +
                    " from " + TableDictionaryManager + " mdm2 where mdm2." + FieldDictionaryCodedTheme +
                    " = '" + _modelPicklist.PicklistField + "'))";
                string queryWhere_1_3 = " AND m." + FieldDictionaryVisible + " = '" + boolYes + "'";
                string queryOrderby_1 = " ORDER BY m." + FieldDictionaryDescription + " ASC";

                string queryFinal = querySelect_1 + queryWhere_1 + querySelect_2 + querySelect_2_join + queryWhere_2 + querySelect_3 + queryWhere_3 + queryOrderBy_3 + queryWhere_1_2 + queryWhere_1_3 + queryOrderby_1;

                SQLiteAsyncConnection currentConnection = da.GetConnectionFromPath(da.PreferedDatabasePath);
                List<Vocabularies> parentVocabs = await currentConnection.QueryAsync<Vocabularies>(queryFinal);

                if (parentVocabs != null && parentVocabs.Count() > 0)
                {
                    //Convert to custom picker
                    _picklistParents = da.GetComboboxListFromVocab(parentVocabs);
                    OnPropertyChanged(nameof(PicklistParents));
                    doesHaveParents = true;
                }

                await currentConnection.CloseAsync();
            }

            return doesHaveParents;
        }
        #endregion
    }
}
