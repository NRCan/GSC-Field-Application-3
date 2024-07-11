﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GSCFieldApp.Views;
using GSCFieldApp.Services.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GSCFieldApp.Models;
using GSCFieldApp.Dictionaries;
using GSCFieldApp.Themes;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Maui.ApplicationModel.Communication;
using System.Xml.Linq;
using System.Reflection;
using Microsoft.Maui.Controls.PlatformConfiguration;
using CommunityToolkit.Maui.Alerts;
using SQLite;
using GSCFieldApp.Services;

namespace GSCFieldApp.ViewModel
{

    [QueryProperty(nameof(FieldLocation), nameof(FieldLocation))]
    [QueryProperty(nameof(Metadata), nameof(Metadata))]
    [QueryProperty(nameof(Station), nameof(Station))]
    public partial class StationViewModel : ObservableObject
    {
        #region INIT
        public FieldThemes FieldThemes { get; set; }
        DataAccess da = new DataAccess();
        ConcatenatedCombobox concat = new ConcatenatedCombobox(); //Use to concatenate values
        private Station _model = new Station();
        private DateTimeOffset _dateGeneric = DateTime.Now; //Default
        public DataIDCalculation idCalculator = new DataIDCalculation();
        private ComboBox _stationType = new ComboBox();
        private ComboBox _stationOutcropQuality = new ComboBox();
        private ComboBox _stationSource = new ComboBox();
        private ComboBox _stationPhysEnv = new ComboBox();

        //Concatenated
        private ComboBoxItem _selectedStationOutcropQuality = new ComboBoxItem();
        private ObservableCollection<ComboBoxItem> _qualityCollection = new ObservableCollection<ComboBoxItem>();

        //Themes
        //private bool _bedrockVisibility = true; //Visibility for extra fields

        //Localize
        public LocalizationResourceManager LocalizationResourceManager
        => LocalizationResourceManager.Instance; // Will be used for in code dynamic local strings

        //Services
        public CommandService commandServ = new CommandService();

        #endregion

        #region PROPERTIES

        [ObservableProperty]
        FieldLocation fieldLocation;

        [ObservableProperty]
        private Metadata metadata;

        [ObservableProperty]
        private Station _station;


        public bool StationTypeVisibility 
        {
            get { return Preferences.Get(nameof(StationTypeVisibility), true); }
            set { Preferences.Set(nameof(StationTypeVisibility), value); }
        }
        public bool StationOutcropVisibility
        {
            get { return Preferences.Get(nameof(StationOutcropVisibility), true); }
            set { Preferences.Set(nameof(StationOutcropVisibility), value); }
        }
        public bool StationGeneralVisibility
        {
            get { return Preferences.Get(nameof(StationGeneralVisibility), true); }
            set { Preferences.Set(nameof(StationGeneralVisibility), value); }
        }
        public bool StationNotesVisibility
        {
            get { return Preferences.Get(nameof(StationNotesVisibility), true); }
            set { Preferences.Set(nameof(StationNotesVisibility), value); }
        }
        public Station Model { get { return _model; } set { _model = value; } }
        public ComboBox StationType { get { return _stationType; } set { _stationType = value; } }
        public ComboBox StationOutcropQuality { get { return _stationOutcropQuality; } set { _stationOutcropQuality = value; } }
        public ComboBox StationSource { get { return _stationSource; } set { _stationSource = value; } }
        public ComboBox StationPhysEnv { get { return _stationPhysEnv; } set { _stationPhysEnv = value; } }

        public ComboBoxItem SelectedStationOutcropQuality 
        { 
            get 
            { 
                return _selectedStationOutcropQuality; 
            } 
            set 
            {
                if (_selectedStationOutcropQuality != value)
                {
                    if (_qualityCollection != null)
                    {
                        if (_qualityCollection.Count > 0 && _qualityCollection[0] == null)
                        {
                            _qualityCollection.RemoveAt(0);
                        }

                        if (value != null && value.itemName != string.Empty)
                        {
                            _qualityCollection.Add(value);
                            _selectedStationOutcropQuality = value;
                            OnPropertyChanged(nameof(QualityCollection));
                        }
                    }


                }
                
            } 
        }
        public ObservableCollection<ComboBoxItem> QualityCollection { get { return _qualityCollection; } set { _qualityCollection = value; } }

        //public bool BedrockVisibility { get { return _bedrockVisibility; } set { _bedrockVisibility = value; } }
        #endregion 

        public StationViewModel()
        {
            FieldThemes = new FieldThemes();
        }

        #region RELAYS

        [RelayCommand]
        public async Task Hide(string visibilityObjectName )
        {
            //Use reflection to parse incoming block to hide
            PropertyInfo? prop = typeof(StationViewModel).GetProperty( visibilityObjectName );

            if (prop != null)
            {
                bool propBool = (bool)prop.GetValue(this);

                // Reverse
                propBool = propBool ? false : true;

                prop.SetValue(this, propBool);
                OnPropertyChanged(visibilityObjectName);
            }

        }

        [RelayCommand]
        public async Task Back()
        {
            //Delete location associated to this station
            _ = await da.DeleteItemAsync(fieldLocation);

            //Android when navigating back, ham menu disapears if / isn't added to path
            await Shell.Current.GoToAsync("../");
        }

        /// <summary>
        /// Will delete a selected item in quality collection box.
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task DeleteItem(ComboBoxItem item)
        {
            if (_qualityCollection.Contains(item))
            {
                _qualityCollection.Remove(item);
                OnPropertyChanged(nameof(QualityCollection));
            }

        }

        [RelayCommand]
        async Task Save()
        {
            //Fill out missing values in model
            await SetModelAsync();

            //Validate if new entry or update
            if (_station != null &&_station.StationAlias != string.Empty && _model.StationID != 0)
            {
                await da.SaveItemAsync(Model, true);
            }
            else
            {
                //Insert new record
                await da.SaveItemAsync(Model, false);
            }

            //Close to be sure
            await da.CloseConnectionAsync();

            //Exit
            //await Shell.Current.GoToAsync($"{nameof(MapPage)}/");
            await Shell.Current.GoToAsync("../");

        }

        /// <summary>
        /// Save button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task SaveStay()
        {
            ////Fill out missing values in model
            //await SetModelAsync();

            ////Validate if new entry or update
            //if (_station != null && _station.StationAlias != string.Empty && _model.StationID != 0)
            //{
            //    await da.SaveItemAsync(Model, true);
            //}
            //else
            //{
            //    //Insert new record
            //    await da.SaveItemAsync(Model, false);

            //}

            ////Close to be sure
            //await da.CloseConnectionAsync();

            ////Show saved message
            //await Toast.Make(LocalizationResourceManager["ToastSaveRecord"].ToString()).Show(CancellationToken.None);

            ////Reset
            //await ResetModelAsync();
            //OnPropertyChanged(nameof(Model));

            //Display a warning to user
            await Shell.Current.DisplayAlert(LocalizationResourceManager["DisplayAlertNotAllowed"].ToString(),
                LocalizationResourceManager["DisplayAlertNotAllowedContent"].ToString(),
                LocalizationResourceManager["GenericButtonOk"].ToString());

        }

        [RelayCommand]
        async Task SaveDelete()
        {
            if (_station != null && _station.StationID != 0)
            {
                await commandServ.DeleteDatabaseItemCommand(DatabaseLiterals.TableNames.station, _station.StationAlias, _station.LocationID);
            }

            //Exit
            await Shell.Current.GoToAsync("../");

        }

        [RelayCommand]
        async Task AddEarthmat()
        {
            //Fill out missing values in model
            await SetModelAsync();

            //Validate if new entry or update
            if (_station != null && _station.StationAlias != string.Empty)
            {
                await da.SaveItemAsync(Model, true);
            }
            else
            {
                //Insert new record
                await da.SaveItemAsync(Model, false);
            }

            //Close to be sure
            await da.CloseConnectionAsync();

            //Navigate to child
            await Shell.Current.GoToAsync($"{nameof(EarthmatPage)}/",
                new Dictionary<string, object>
                {
                    [nameof(Earthmaterial)] = null,
                    [nameof(Station)] = Model
                }
            );
        }



        #endregion

        #region METHODS

        /// <summary>
        /// Will refill the form with existing values for update/editing purposes
        /// </summary>
        /// <returns></returns>
        public async Task Load()
        {
            if (_station != null && _station.StationAlias != string.Empty)
            {
                //Set model like actual record
                _model = _station;

                //Refresh
                OnPropertyChanged(nameof(Model));

                #region Pickers
                //Select values in pickers
                foreach (ComboBoxItem cbox in StationType.cboxItems)
                {
                    if (cbox.itemValue == _station.StationObsType)
                    {
                        StationType.cboxDefaultItemIndex = StationType.cboxItems.IndexOf(cbox);
                        break;
                    }
                }
                OnPropertyChanged(nameof(StationType));

                foreach (ComboBoxItem cbox in StationSource.cboxItems)
                {
                    if (cbox.itemValue == _station.StationObsSource)
                    {
                        StationSource.cboxDefaultItemIndex = StationSource.cboxItems.IndexOf(cbox);
                        break;
                    }
                }
                OnPropertyChanged(nameof(StationSource));

                foreach (ComboBoxItem cbox in StationPhysEnv.cboxItems)
                {
                    if (cbox.itemValue == _station.StationPhysEnv)
                    {
                        StationPhysEnv.cboxDefaultItemIndex = StationPhysEnv.cboxItems.IndexOf(cbox);
                        break;
                    }
                }
                OnPropertyChanged(nameof(StationPhysEnv));
                #endregion

                //Piped value field
                List<string> qualities = ConcatenatedCombobox.UnpipeString(_station.StationOCQuality);
                _qualityCollection.Clear(); //Clear any possible values first
                foreach (ComboBoxItem cbox in StationOutcropQuality.cboxItems)
                {
                    if (qualities.Contains(cbox.itemValue) && !_qualityCollection.Contains(cbox))
                    {
                        _qualityCollection.Add(cbox);
                    }
                }
                OnPropertyChanged(nameof(QualityCollection));
            }
        }

        public async Task FillPickers()
        {

            _stationType = await FillAPicker(DatabaseLiterals.FieldStationObsType);
            _stationOutcropQuality = await FillAPicker(DatabaseLiterals.FieldStationOCQuality);
            _stationSource = await FillAPicker(DatabaseLiterals.FieldStationObsSource);
            _stationPhysEnv = await FillAPicker(DatabaseLiterals.FieldStationPhysEnv);

            OnPropertyChanged(nameof(StationType));
            OnPropertyChanged(nameof(StationOutcropQuality));
            OnPropertyChanged(nameof(StationSource));
            OnPropertyChanged(nameof(StationPhysEnv));

        }

        /// <summary>
        /// Will fill the project type combobox
        /// </summary>
        private async Task<ComboBox> FillAPicker(string fieldName)
        {
            //Make sure to user default database rather then the prefered one. This one will always be there.
            return await da.GetComboboxListWithVocabAsync(DatabaseLiterals.TableStation, fieldName);

        }

        /// <summary>
        /// Will fill out missing fields for model. Default auto-calculated values
        /// </summary>
        private async Task SetModelAsync()
        {
            //Make sure it's for a new field book
            if (Model.StationID == 0 && fieldLocation != null)
            {
                //Get current application version
                Model.LocationID = fieldLocation.LocationID;
                Model.StationAlias = await idCalculator.CalculateStationAliasAsync(DateTime.Now);
                Model.StationVisitDate = CalculateStationDate(); //Calculate new value
                Model.StationVisitTime = CalculateStationTime(); //Calculate new value
            }

            //Process pickers
            if (StationType.cboxDefaultItemIndex != -1 && StationType.cboxItems.Count > 0)
            {
                Model.StationObsType = StationType.cboxItems[StationType.cboxDefaultItemIndex].itemValue;
            }
            if (StationOutcropQuality.cboxDefaultItemIndex != -1 && QualityCollection != null && StationOutcropQuality.cboxItems.Count > 0)
            {
                Model.StationOCQuality = ConcatenatedCombobox.PipeValues(QualityCollection); //process list of values so they are concatenated.
            }
            if (StationSource.cboxDefaultItemIndex != -1 && StationSource.cboxItems.Count > 0)
            {
                Model.StationObsSource = StationSource.cboxItems[StationSource.cboxDefaultItemIndex].itemValue;
            }
            if (StationPhysEnv.cboxDefaultItemIndex != -1 && StationPhysEnv.cboxItems.Count > 0)
            {
                Model.StationPhysEnv = StationPhysEnv.cboxItems[StationPhysEnv.cboxDefaultItemIndex].itemValue;
            }

            //Process Air Photo and Traverse numbers
            FillAirPhotoTraverseNo(); 
        }

        /// <summary>
        /// TODO: Make sure this feature is still relevant. Not sure many people are using it.
        /// Will prefill air photo number and traverse number from the last entered values by user.
        /// </summary>
        private void FillAirPhotoTraverseNo()
        {
            ////Special case for air photo and traverse numbers, get the last numbers
            //string tableName = Dictionaries.DatabaseLiterals.TableStation;
            //string querySelectFrom = "SELECT * FROM " + tableName;
            //string queryOrder1 = " ORDER BY " + tableName + "." + Dictionaries.DatabaseLiterals.FieldStationVisitDate + " DESC";
            //string queryOrder2 = ", " + tableName + "." + Dictionaries.DatabaseLiterals.FieldStationVisitTime + " DESC";
            //string queryLimit = " LIMIT 1";
            //string finaleQuery = querySelectFrom + queryOrder1 + queryOrder2 + queryLimit;

            //List<object> stationTableRaw = accessData.ReadTable(StationModel.GetType(), finaleQuery);
            //IEnumerable<Station> stationFiltered = stationTableRaw.Cast<Station>(); //Cast to proper list type
            //if (stationFiltered.Count() != 0 || stationFiltered != null)
            //{
            //    foreach (Station sts in stationFiltered)
            //    {
            //        _stationTravNo = sts.StationTravNo.ToString();

            //        //Make check on date if newer, increment traverse no. if wanted by user
            //        if (localSetting.GetSettingValue(ApplicationLiterals.KeywordStationTraverseNo) == null ||
            //            (localSetting.GetSettingValue(ApplicationLiterals.KeywordStationTraverseNo) != null &&
            //            localSetting.GetBoolSettingValue(ApplicationLiterals.KeywordStationTraverseNo)))
            //        {
            //            string currentDate = DateTime.Now.ToShortDateString();
            //            DateTime lastStationDate = DateTime.Parse(sts.StationVisitDate);
            //            DateTime currentDateDT = DateTime.Parse(currentDate);
            //            if (lastStationDate != null && currentDateDT != null)
            //            {
            //                int dateComparisonResult = DateTime.Compare(lastStationDate, currentDateDT);
            //                if (lastStationDate != null && dateComparisonResult < 0)
            //                {
            //                    _stationTravNo = (sts.StationTravNo + 1).ToString();
            //                }
            //            }

            //        }




            //        _airno = sts.StationAirNo;
            //    }

            //}

            //RaisePropertyChanged("TraverseNo");
            //RaisePropertyChanged("AirPhoto");
        }

        /// <summary>
        /// Will reset model fields to default just like it's a new record
        /// </summary>
        /// <returns></returns>
        private async Task ResetModelAsync()
        {

            //Reset model
            if (Model.StationID == 0)
            {
                //Get current application version
                Model.LocationID = fieldLocation.LocationID;
                Model.StationAlias = await idCalculator.CalculateStationAliasAsync(DateTime.Now);
                Model.StationVisitDate = CalculateStationDate(); //Calculate new value
                Model.StationVisitTime = CalculateStationTime(); //Calculate new value
            }
            else if (Model.LocationID != null)
            {
                // if coming from field notes on a record edit that needs to be saved as a new record with stay/save
                Model.StationAlias = await idCalculator.CalculateStationAliasAsync(DateTime.Now);
                Model.StationVisitDate = CalculateStationDate(); //Calculate new value
                Model.StationVisitTime = CalculateStationTime(); //Calculate new value
            }

            Model.StationID = 0;

        }

        /// <summary>
        /// Will make a quick station record in station table, from a given xy position. 
        /// XY will be used to create a quick location first
        /// </summary>
        /// <param name="fieldLocation"></param>
        /// <returns></returns>
        public async Task<Station> QuickStation(int locationID)
        {
            //Spoof a location object with only an ID so SetModelAsync works well
            FieldLocation fl = new FieldLocation();
            fl.LocationID = locationID;
            fieldLocation = fl;

            //Fill out model and save new record
            await SetModelAsync();
            Station quickStation = await da.SaveItemAsync(Model, false) as Station;

            return quickStation;
        }

        #endregion

        #region CALCULATIONS

        public string CalculateStationDate()
        {
            return String.Format("{0:yyyy-MM-dd}", _dateGeneric); ;
        }

        public string CalculateStationTime()
        {
            return String.Format("{0:HH:mm:ss t}", _dateGeneric); ;
        }

        #endregion

        #region EVENTS

        
        #endregion
    }
}
