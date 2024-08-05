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
using System.Security.Cryptography;
namespace GSCFieldApp.ViewModel
{
    [QueryProperty(nameof(DrillHole), nameof(DrillHole))]
    [QueryProperty(nameof(FieldLocation), nameof(FieldLocation))]
    public partial class DrillHoleViewModel: FieldAppPageHelper
    {
        #region INIT

        private DrillHole _model = new DrillHole();
        private ComboBox _drillType = new ComboBox();
        private ComboBox _drillUnits = new ComboBox();
        private ComboBox _drillHoleSizes = new ComboBox();

        #endregion

        #region PROPERTIES

        [ObservableProperty]
        private DrillHole _drillHole;

        [ObservableProperty]
        private FieldLocation _fieldLocation;

        public DrillHole Model { get { return _model; } set { _model = value; } }

        public bool DrillHoleContextVisibility
        {
            get { return Preferences.Get(nameof(DrillHoleContextVisibility), true); }
            set { Preferences.Set(nameof(DrillHoleContextVisibility), value); }
        }

        public bool DrillHoleMetricsVisibility
        {
            get { return Preferences.Get(nameof(DrillHoleMetricsVisibility), true); }
            set { Preferences.Set(nameof(DrillHoleMetricsVisibility), value); }
        }

        public bool DrillHoleLogVisibility
        {
            get { return Preferences.Get(nameof(DrillHoleLogVisibility), true); }
            set { Preferences.Set(nameof(DrillHoleLogVisibility), value); }
        }

        public bool DrillHoleGeneralVisibility
        {
            get { return Preferences.Get(nameof(DrillHoleGeneralVisibility), true); }
            set { Preferences.Set(nameof(DrillHoleGeneralVisibility), value); }
        }

        public ComboBox DrillType { get { return _drillType; } set { _drillType = value; } }
        public ComboBox DrillUnits { get { return _drillUnits; } set { _drillUnits = value; } }
        public ComboBox DrilHoleSizes { get { return _drillHoleSizes; } set { _drillHoleSizes = value; } }
        #endregion

        #region RELAYS
        [RelayCommand]
        public async Task Back()
        {
            //Exit
            await NavigateToFieldNotes(TableNames.drill, false);

        }

        /// <summary>
        /// Save button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task Save()
        {
            //Save
            await SetAndSaveModelAsync();

            //Exit
            await NavigateToFieldNotes(TableNames.drill);
        }

        /// <summary>
        /// Save button command
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        async Task SaveStay()
        {
            //Save
            await SetAndSaveModelAsync();

            //Show saved message
            await Toast.Make(LocalizationResourceManager["ToastSaveRecord"].ToString()).Show(CancellationToken.None);

            //Reset
            await ResetModelAsync();
            OnPropertyChanged(nameof(Model));


        }

        [RelayCommand]
        async Task SaveDelete()
        {
            if (_model.DrillID != 0)
            {
                await commandServ.DeleteDatabaseItemCommand(TableNames.drill, _model.DrillIDName, _model.DrillID);
            }

            //Exit
            await NavigateToFieldNotes(TableNames.drill);

        }


        #endregion

        #region METHODS


        public async Task SetAndSaveModelAsync()
        {
            //Fill out missing values in model
            await SetModelAsync();

            //Validate if new entry or update
            if (_drillHole != null && _drillHole.DrillIDName != string.Empty && _model.DrillID != 0)
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
        }

        /// <summary>
        /// Will fill out missing fields for model. Default auto-calculated values
        /// Done before actually saving
        /// </summary>
        private async Task SetModelAsync()
        {

        }

        /// <summary>
        /// Will reset model fields to default just like it's a new record
        /// </summary>
        /// <returns></returns>
        private async Task ResetModelAsync()
        {

            //Reset model
            if (_fieldLocation != null)
            {
                // if coming from em notes, calculate new alias
                Model.DrillLocationID = _fieldLocation.LocationID;
                DateTime locationDate = DateTime.Parse(_fieldLocation.LocationTimestamp);
                Model.DrillIDName = await idCalculator.CalculateDrillAliasAsync(locationDate, _fieldLocation.LocationID);
            }

            else if (Model.DrillLocationID != null)
            {
                // if coming from field notes on a record edit that needs to be saved as a new record with stay/save
                SQLiteAsyncConnection currentConnection = da.GetConnectionFromPath(da.PreferedDatabasePath);
                List<FieldLocation> parent = await currentConnection.Table<FieldLocation>().Where(e => e.LocationID == Model.DrillLocationID).ToListAsync();
                await currentConnection.CloseAsync();
                DateTime locationDate = DateTime.Parse(parent.First().LocationTimestamp);
                Model.DrillIDName = await idCalculator.CalculateDrillAliasAsync(locationDate, parent.First().LocationID);
            }

            Model.DrillID = 0;

        }

        /// <summary>
        /// Will refill the form with existing values for update/editing purposes
        /// </summary>
        /// <returns></returns>
        public async Task Load()
        {
            if (_drillHole != null && _drillHole.DrillIDName != string.Empty)
            {
                //Set model like actual record
                _model = _drillHole;

                //Refresh
                OnPropertyChanged(nameof(Model));

            }
        }

        /// <summary>
        /// Will fill all picker controls
        /// TODO: make sure this whole thing doesn't slow too much form rendering
        /// </summary>
        /// <returns></returns>
        public async Task FillPickers()
        {

            _drillType = await FillAPicker(FieldDrillType);
            OnPropertyChanged(nameof(DrillType));

            _drillUnits = await FillAPicker(FieldDrillUnit);
            OnPropertyChanged(nameof(DrillUnits));

            _drillHoleSizes = await FillAPicker(FieldDrillHoleSize);
            OnPropertyChanged(nameof(DrilHoleSizes));
        }


        /// <summary>
        /// Generic method to fill a needed picker control with vocabulary
        /// </summary>
        private async Task<ComboBox> FillAPicker(string fieldName, string extraField = "")
        {
            //Make sure to user default database rather then the prefered one. This one will always be there.
            return await da.GetComboboxListWithVocabAsync(TableDrillHoles, fieldName, extraField);

        }

        /// <summary>
        /// Will initialize the model with needed calculated fields
        /// </summary>
        /// <returns></returns>
        public async Task InitModel()
        {
            if (Model != null && Model.DrillID == 0 && _fieldLocation != null)
            {
                //Get current application version
                Model.DrillLocationID = _fieldLocation.LocationID;
                DateTime parentDate = DateTime.Parse(_fieldLocation.LocationTimestamp);
                Model.DrillIDName = await idCalculator.CalculateDrillAliasAsync(parentDate, _fieldLocation.LocationID);
                OnPropertyChanged(nameof(Model));

            }

        }

        #endregion
    }
}
