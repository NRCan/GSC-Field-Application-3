﻿using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GSCFieldApp.Services.DatabaseServices;
using GSCFieldApp.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.ObjectModel;
using GSCFieldApp.Models;
using Microsoft.Maui.Controls.PlatformConfiguration;
using SQLite;
using System.Resources;
using GSCFieldApp.Dictionaries;

namespace GSCFieldApp.ViewModel
{
    public partial class FieldBooksViewModel: ObservableObject
    {
        //UI
        private bool _noFieldBookWatermark = false;
        public ObservableCollection<FieldBooks> _projectCollection = new ObservableCollection<FieldBooks>();

        //Data service
        private DataAccess da = new DataAccess();

        //Models
        readonly Station stationModel = new Station();
        readonly Metadata metadataModel = new Metadata();

        public bool NoFieldBookWatermark { get { return _noFieldBookWatermark; } set { _noFieldBookWatermark = value; } }
        public ObservableCollection<FieldBooks> ProjectCollection
        {
            get { return _projectCollection; }
        }
        public FieldBooksViewModel() 
        {
            //Fill list view of projects
            FillProjectCollectionAsync();
        }

        #region RELAY COMMANDS

        [RelayCommand]
        async Task AddFieldBook()
        {
            await Shell.Current.GoToAsync($"{nameof(FieldBookPage)}");
        }

        [RelayCommand]
        async Task UploadFieldBook()
        {
            await Shell.Current.DisplayAlert("Alert", "Not yet implemented", "OK");
        }

        [RelayCommand]
        async Task DownloadFieldBook()
        {
            await Shell.Current.DisplayAlert("Alert", "Not yet implemented", "OK");
        }

        [RelayCommand]
        async Task UpdateFieldBook()
        {
            await Shell.Current.DisplayAlert("Alert", "Not yet implemented", "OK");
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Will fill the project collection with information related to it
        /// </summary>
        private async void FillProjectCollectionAsync()
        {
            _projectCollection.Clear();

            List<string> invalidFieldBookToDelete = new List<string>();

            //Iterate through local state folder
            string[] fileList = Directory.GetFiles(FileSystem.Current.AppDataDirectory);

            foreach (string sf in fileList)
            {

                //Get the databases but not the main default one
                if ((sf.Contains(DatabaseLiterals.DBTypeSqlite) || sf.Contains(DatabaseLiterals.DBTypeSqliteDeprecated))
                    && !sf.Contains(DatabaseLiterals.DBName))
                {

                    //Connect to found database and retrive some information from it
                    FieldBooks currentBook = new FieldBooks();
                    SQLiteAsyncConnection currentConnection = da.GetConnectionFromPath(sf);

                    //Get metadata records
                    List<Metadata> metadataTableRows = await currentConnection.Table<Metadata>().ToListAsync();
                    foreach (Metadata m in metadataTableRows)
                    {
                        //For metadata
                        Metadata met = m as Metadata;
                        currentBook.CreateDate = met.StartDate;
                        currentBook.GeologistGeolcode = met.Geologist + "[" + met.UserCode + "]";
                        currentBook.ProjectPath = FileSystem.Current.AppDataDirectory;
                        currentBook.ProjectDBPath = sf;
                        currentBook.metadataForProject = m as Metadata;
                    }

                    //For stations
                    string stationQuerySelect = "SELECT *";
                    string stationQueryFrom = " FROM " + DatabaseLiterals.TableStation;
                    string stationQueryWhere = " WHERE " + DatabaseLiterals.TableStation + "." + DatabaseLiterals.FieldStationAlias + " NOT LIKE '%" + DatabaseLiterals.KeywordStationWaypoint + "%'";
                    string stationQueryFinal = stationQuerySelect + stationQueryFrom + stationQueryWhere;
                    List<Station> stationCountResult = await currentConnection.Table<Station>().ToListAsync();
                    if (stationCountResult != null && stationCountResult.Count > 0)
                    {
                        currentBook.StationNumber = stationCountResult.Count.ToString();
                    }
                    else if (stationCountResult != null && stationCountResult.Count == 0)
                    {
                        currentBook.StationNumber = "0";
                    }
                    else
                    {
                        currentBook.StationNumber = "?";
                    }
                    if (stationCountResult.Count != 0)
                    {
                        Station lastStation = (Station)stationCountResult[stationCountResult.Count - 1];
                        currentBook.StationLastEntered = lastStation.StationAlias;
                    }

                    _projectCollection.Add(currentBook);

                    await currentConnection.CloseAsync(); 
                    
                }
                
            }

            ////Select the current active project, so it's highlighted in the list view
            //ResourceLoader loadLocal = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            //if (_projectCollection.Count == 0)
            //{

            //    _noFieldBookWatermark = true;

            //    //Send event about missing field books.
            //    fieldBooksUpdate?.Invoke(this, false);

            //}
            //else
            //{
            //    SelectActiveProject();
            //    _noFieldBookWatermark = false;

            //    //Send event about missing field books.
            //    fieldBooksUpdate?.Invoke(this, true);
            //}

        }


        #endregion
    }
}
