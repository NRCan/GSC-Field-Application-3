﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GSCFieldApp.Models;
using GSCFieldApp.Services.DatabaseServices;
using GSCFieldApp.Themes;
using GSCFieldApp.Views;
using GSCFieldApp.Services;
using GSCFieldApp.Dictionaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using SQLite;
using System.Data;

namespace GSCFieldApp.ViewModel
{
    [QueryProperty(nameof(Sample), nameof(Sample))]
    [QueryProperty(nameof(Earthmaterial), nameof(Earthmaterial))]
    public partial class SampleViewModel : ObservableObject
    {

        #region INIT
        DataAccess da = new DataAccess();
        ConcatenatedCombobox concat = new ConcatenatedCombobox(); //Use to concatenate values
        public DataIDCalculation idCalculator = new DataIDCalculation();
        private Sample _model = new Sample();
        private ComboBox _samplePurpose = new ComboBox();
        private ComboBox _sampleType = new ComboBox();
        private ComboBox _sampleCorePortion = new ComboBox();
        private ComboBox _sampleFormat = new ComboBox();
        private ComboBox _sampleSurface = new ComboBox();
        private ComboBox _sampleQuality = new ComboBox();
        private ComboBox _sampleState = new ComboBox();
        private ComboBox _sampleHorizon = new ComboBox();
        private bool _isSampleDuplicate = false;

        //Concatenated
        private ComboBoxItem _selectedSamplePurpose = new ComboBoxItem();
        private ObservableCollection<ComboBoxItem> _purposeCollection = new ObservableCollection<ComboBoxItem>();
        //Localize
        public LocalizationResourceManager LocalizationResourceManager
        => LocalizationResourceManager.Instance; // Will be used for in code dynamic local strings

        //Services
        public CommandService commandServ = new CommandService();

        #endregion

        #region PROPERTIES

        [ObservableProperty]
        private Earthmaterial _earthmaterial;

        [ObservableProperty]
        private Sample _sample;

        public FieldThemes FieldThemes { get; set; } //Enable/Disable certain controls based on work type

        public Sample Model { get { return _model; } set { _model = value; } }

        public bool SampleDescVisibility
        {
            get { return Preferences.Get(nameof(SampleDescVisibility), true); }
            set { Preferences.Set(nameof(SampleDescVisibility), value); }
        }

        public bool SampleCoreVisibility
        {
            get { return Preferences.Get(nameof(SampleCoreVisibility), true); }
            set { Preferences.Set(nameof(SampleCoreVisibility), value); }
        }

        public bool SampleOrientVisibility
        {
            get { return Preferences.Get(nameof(SampleOrientVisibility), true); }
            set { Preferences.Set(nameof(SampleOrientVisibility), value); }
        }

        public bool SampleStateVisibility
        {
            get { return Preferences.Get(nameof(SampleStateVisibility), true); }
            set { Preferences.Set(nameof(SampleStateVisibility), value); }
        }

        public bool SampleGeneralVisibility
        {
            get { return Preferences.Get(nameof(SampleGeneralVisibility), true); }
            set { Preferences.Set(nameof(SampleGeneralVisibility), value); }
        }

        public ComboBox SampleType { get { return _sampleType; } set { _sampleType = value; } }

        public ComboBox SamplePurpose { get { return _samplePurpose; } set { _samplePurpose = value; } }
        public ObservableCollection<ComboBoxItem> SamplePurposeCollection { get { return _purposeCollection; } set { _purposeCollection = value; OnPropertyChanged(nameof(SamplePurposeCollection)); } }
        public ComboBoxItem SelectedSamplePurpose
        {
            get
            {
                return _selectedSamplePurpose;
            }
            set
            {
                if (_selectedSamplePurpose != value)
                {
                    if (_purposeCollection != null)
                    {
                        if (_purposeCollection.Count > 0 && _purposeCollection[0] == null)
                        {
                            _purposeCollection.RemoveAt(0);
                        }
                        if (value != null && value.itemName != string.Empty)
                        {
                            _purposeCollection.Add(value);
                            _selectedSamplePurpose = value;
                            OnPropertyChanged(nameof(SelectedSamplePurpose));
                            
                        }

                    }


                }

            }
        }

        public bool IsSampleDuplicate { get { return _isSampleDuplicate; } set { _isSampleDuplicate = value; } }

        public ComboBox SampleCorePortion { get { return _sampleCorePortion; } set { _sampleCorePortion = value; } }
        public ComboBox SampleFormat { get { return _sampleFormat; } set { _sampleFormat = value; } }
        public ComboBox SampleSurface { get { return _sampleSurface; } set { _sampleSurface = value; } }
        public ComboBox SampleQuality { get { return _sampleQuality; } set { _sampleQuality = value; } }
        public ComboBox SampleState { get { return _sampleState; } set { _sampleState = value; } }
        public ComboBox SampleHorizon { get { return _sampleHorizon; } set { _sampleHorizon = value; } }
        #endregion

        public SampleViewModel() 
        {
            //Init new field theme
            FieldThemes = new FieldThemes();
        }

        #region RELAYS
        /// <summary>
        /// Back button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]

        public async Task Back()
        {
            //Android when navigating back, ham menu disapears if / isn't added to path
            await Shell.Current.GoToAsync($"{nameof(FieldNotesPage)}/");
        }

        /// <summary>
        /// Save button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task Save()
        {
            //Fill out missing values in model
            await SetModelAsync();

            //Validate if new entry or update
            if (_sample != null && _sample.SampleName != string.Empty && _model.SampleID != 0)
            {

                await da.SaveItemAsync(Model, true);
            }
            else
            {
                //New entry coming from parent form
                //Insert new record
                await da.SaveItemAsync(Model, false);
            }

            //Close to be sure
            await da.CloseConnectionAsync();

            //Exit
            await Shell.Current.GoToAsync($"../{nameof(FieldNotesPage)}");
            //await Shell.Current.GoToAsync("../");
        }

        /// <summary>
        /// Save button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task SaveStay()
        {
            //Fill out missing values in model
            await SetModelAsync();

            //Validate if new entry or update
            if (_sample != null && _sample.SampleName != string.Empty && _model.SampleID != 0)
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

            //Show saved message
            await Toast.Make(LocalizationResourceManager["ToastSaveRecord"].ToString()).Show(CancellationToken.None);

            //Reset
            await ResetModelAsync();
            OnPropertyChanged(nameof(Model));


        }

        [RelayCommand]
        async Task SaveDelete()
        {
            if (_model.SampleID != 0)
            {
                await commandServ.DeleteDatabaseItemCommand(DatabaseLiterals.TableNames.sample, _model.SampleName, _model.SampleID);
            }

            //Exit
            await Shell.Current.GoToAsync($"/{nameof(FieldNotesPage)}/");
            //await Shell.Current.GoToAsync("../");

        }

        /// <summary>
        /// Will calculate the Core To value based on entered Core From (in m) and Core length (in cm)
        /// It'll need to be adjusted since the units are different between from and length.
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public void SampleCoreCalculatTo()
        {
            //Recalculate To value
            Model.SampleCoreTo = Model.SampleCoreFrom + Model.SampleCoreLength / 100;
            OnPropertyChanged(nameof(Model));
        }
        #endregion

        #region METHODS

        /// <summary>
        /// Initialize all pickers. 
        /// To save loading time, process only those needed based on work type
        /// </summary>
        /// <returns></returns>
        public async Task FillPickers()
        {
            _sampleType = await FillAPicker(DatabaseLiterals.FieldSampleType);
            _samplePurpose = await FillAPicker(DatabaseLiterals.FieldSamplePurpose);
            OnPropertyChanged(nameof(SampleType));
            OnPropertyChanged(nameof(SamplePurpose));

            //Bedrock pickers
            if (Preferences.ContainsKey(nameof(DatabaseLiterals.FieldUserInfoFWorkType))
                && Preferences.Get(nameof(DatabaseLiterals.FieldUserInfoFWorkType), "").ToString().Contains(DatabaseLiterals.ApplicationThemeBedrock))
            {
                _sampleCorePortion = await FillAPicker(DatabaseLiterals.FieldSampleCoreSize);
                _sampleFormat = await FillAPicker(DatabaseLiterals.FieldSampleFormat);
                _sampleSurface = await FillAPicker(DatabaseLiterals.FieldSampleSurface);
                OnPropertyChanged(nameof(SampleCorePortion));
                OnPropertyChanged(nameof(SampleFormat));
                OnPropertyChanged(nameof(SampleSurface));
            }

            //Surficial pickers
            if (Preferences.ContainsKey(nameof(DatabaseLiterals.FieldUserInfoFWorkType))
                && Preferences.Get(nameof(DatabaseLiterals.FieldUserInfoFWorkType), "").ToString() == DatabaseLiterals.ApplicationThemeSurficial)
            {
                _sampleQuality = await FillAPicker(DatabaseLiterals.FieldSampleQuality);
                _sampleState = await FillAPicker(DatabaseLiterals.FieldSampleState);
                _sampleHorizon = await FillAPicker(DatabaseLiterals.FieldSampleHorizon);
                OnPropertyChanged(nameof(SampleQuality));
                OnPropertyChanged(nameof(SampleState));
                OnPropertyChanged(nameof(SampleHorizon));
            }

        }

        /// <summary>
        /// Will fill the project type combobox
        /// </summary>
        private async Task<ComboBox> FillAPicker(string fieldName)
        {
            //Make sure to user default database rather then the prefered one. This one will always be there.
            return await da.GetComboboxListWithVocabAsync(DatabaseLiterals.TableSample, fieldName);

        }

        /// <summary>
        /// Will fill out missing fields for model. Default auto-calculated values
        /// Done before actually saving
        /// </summary>
        private async Task SetModelAsync()
        {
            //Make sure it's for a new field book
            if (Model.SampleID == 0 && _earthmaterial != null)
            {
                //Get current application version
                Model.SampleEarthmatID = _earthmaterial.EarthMatID;
                Model.SampleName = await idCalculator.CalculateSampleAliasAsync(_earthmaterial.EarthMatID, _earthmaterial.EarthMatName);
            }

            #region Process pickers
            if (SamplePurposeCollection.Count > 0)
            {
                Model.SamplePurpose = ConcatenatedCombobox.PipeValues(SamplePurposeCollection); //process list of values so they are concatenated.
            }

            #endregion
        }

        /// <summary>
        /// Will reset model fields to default just like it's a new record
        /// </summary>
        /// <returns></returns>
        private async Task ResetModelAsync()
        {

            //Reset model
            if (_earthmaterial != null)
            {
                // if coming from station notes, calculate new alias
                Model.SampleEarthmatID = _earthmaterial.EarthMatID;
                Model.SampleName = await idCalculator.CalculateSampleAliasAsync(_earthmaterial.EarthMatID, _earthmaterial.EarthMatName);
            }
            else if (Model.SampleEarthmatID != null)
            {
                // if coming from field notes on a record edit that needs to be saved as a new record with stay/save
                SQLiteAsyncConnection currentConnection = da.GetConnectionFromPath(da.PreferedDatabasePath);
                List<Earthmaterial> parentAlias = await currentConnection.Table<Earthmaterial>().Where(e => e.EarthMatID == Model.SampleEarthmatID).ToListAsync();
                await currentConnection.CloseAsync();
                Model.SampleName = await idCalculator.CalculateEarthmatAliasAsync(Model.SampleEarthmatID, parentAlias.First().EarthMatName);
            }

            Model.SampleID = 0;

        }

        /// <summary>
        /// Will refill the form with existing values for update/editing purposes
        /// </summary>
        /// <returns></returns>
        public async Task Load()
        {
            if (_sample != null && _sample.SampleName != string.Empty)
            {
                //Set model like actual record
                _model = _sample;

                //Refresh
                OnPropertyChanged(nameof(Model));

                #region Pickers
                //Select values in pickers
                List<string> bfs = ConcatenatedCombobox.UnpipeString(_sample.SamplePurpose);
                _purposeCollection.Clear(); //Clear any possible values first
                foreach (ComboBoxItem cbox in SamplePurpose.cboxItems)
                {
                    if (bfs.Contains(cbox.itemValue) && !_purposeCollection.Contains(cbox))
                    {
                        _purposeCollection.Add(cbox);
                    }
                }
                OnPropertyChanged(nameof(SamplePurpose));

                #endregion

            }
        }

        /// <summary>
        /// Will show a quick reminder to take a duplicate for
        /// surficial geologist every 15 samples.
        /// </summary>
        public async Task<bool> DuplicateReminder()
        {
            bool shouldShowReminder = false;

            if (Preferences.ContainsKey(nameof(DatabaseLiterals.FieldUserInfoFWorkType))
                && Preferences.Get(nameof(DatabaseLiterals.FieldUserInfoFWorkType), "").ToString() == DatabaseLiterals.ApplicationThemeSurficial)
            {
                Sample sampleModel = new Sample();
                int sampleCount = await da.GetTableCount(Model.GetType());

                if (sampleCount % 15 == 0 && sampleCount != 0)
                {
                    shouldShowReminder = true;
                }
            }

            return shouldShowReminder;

        }


        #endregion
    }
}
