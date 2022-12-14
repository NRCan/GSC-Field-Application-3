﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GSCFieldApp.Services.DatabaseServices;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using Windows.UI.Xaml.Input;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Symbology;
using GSCFieldApp.Dictionaries;
using Windows.UI;
using Symbol = Windows.UI.Xaml.Controls.Symbol;
using Windows.ApplicationModel.Resources;

namespace GSCFieldApp.Views
{

    public sealed partial class MapPage : Page
    {
        #region INIT
        public Map esriMap;
        public bool mapsLoaded = false;
        readonly DataLocalSettings localSetting = new DataLocalSettings();

        //UI headers enable/disable colors
        private readonly string resourceNameGridColor = "MapViewGridColor";

        //Options
        //bool tapMode = false;

        #endregion

        public MapPage()
        {


            localSetting.SetSettingValue(ApplicationLiterals.KeywordMapViewGrid, true);

            this.InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;

            this.Loaded -= MapPage_Loaded;
            this.Loaded += MapPage_Loaded;

        }

        /// <summary>
        /// Detect whenever the app is brought back on main thread. 
        /// Doing so we force a reset of the GPS 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Current_Resuming(object sender, object e)
        {
            ViewModel.ResetLocationGraphic();
            await ViewModel.SetGPS();

        }

        #region EVENTS


        /// <summary>
        /// An event called when the map has been loaded. 
        /// Will set the lat long grid (can only be set after layer are loaded)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapPage_Loaded(object sender, RoutedEventArgs e)
        {


            //Set navigation that will add back new layers
            try
            {
                if (!ViewModel.userHasTurnedGPSOff && ViewModel._currentMSGeoposition == null)
                {
                    ViewModel.StartLocationRing();
                }

                //Refresh
                //SetBackgroundGrid();
                ViewModel.DisplayPointAndLabelsAsync(myMapView);

                Task navigateToLocationTask = ViewModel.SetMapView(myMapView);

                //DisplayLatLongGrid();
                mapsLoaded = true;
                //UpdateGrid();
            }
            catch (Exception er)
            {
                ResourceLoader local = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();


                ContentDialog defaultEventLocationDialog = new ContentDialog()
                {
                    Title = local.GetString("MapPageDialogLocationTitle"),
                    Content = er.Message + "; " + er.StackTrace,
                    CloseButtonText = local.GetString("GenericDialog_ButtonOK")
                };
                defaultEventLocationDialog.Style = (Style)Application.Current.Resources["DeleteDialog"];
                Services.ContentDialogMaker.CreateContentDialog(defaultEventLocationDialog, true);

            }

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        /// <summary>
        /// Triggered when user is going out of the map page
        /// Then keep some values in internal settings
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (ViewModel.currentMapView != null)
            {
                ViewModel.currentMapView.CancelSetViewpointOperations();
            }
            
            base.OnNavigatingFrom(e);
        }

        /// <summary>
        /// Toggle screen information on/off
        /// Includes:
        ///     coordinate information in top left corner
        ///     scale information in lower left corner
        ///     latitude / longitude grid with labels
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapInfoButtonClicked(object sender, RoutedEventArgs e)
        {
            //Hide or show coordinates, accuracy, and projection info when clicked
            MapCoordinateInfo.Visibility = (MapCoordinateInfo.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
            MapScaleInfo.Visibility = (MapScaleInfo.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
            localSetting.SetSettingValue(ApplicationLiterals.KeywordMapViewGrid, ((bool)localSetting.GetSettingValue(ApplicationLiterals.KeywordMapViewGrid) == true ? false : true));
            if (myMapView.Grid != null)
            {
                myMapView.Grid.IsVisible = ((bool)localSetting.GetSettingValue(ApplicationLiterals.KeywordMapViewGrid) == false ? false : true);
            }

            MapCoordinateInfo2.Visibility = (MapCoordinateInfo2.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
            MapCoordinateInfo3.Visibility = (MapCoordinateInfo3.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);

            //Try and enforce whole map view redraw see #176
            //ViewModel.esriMap = null;
            //ViewModel.currentMapView = null;
            //mapsLoaded = false;
            //await ViewModel.SetMapView(myMapView);

        }

        /// <summary>
        /// Will triggered a delete action on the selected layer. It'll be removed from the map and deleted from the local state folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapDeleteIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender != null && mapsLoaded)
            {
                this.mapPageLayerFlyout.Hide();

                ViewModel.DeleteLayersAsync(true);
            }
        }

        /// <summary>
        /// Will be triggered when user toggles the switch. This methods needs to be here, else in the view model it doesn't work since
        /// the event is inside a data template in the xaml.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mapFileName_Toggled(object sender, RoutedEventArgs e)
        {

            if (sender != null)
            {
                ToggleSwitch senderToggle = sender as ToggleSwitch;
                ViewModel.SetLayerVisibility(senderToggle);
            }

        }

        #endregion

        #region METHODS
        /// <summary>
        /// Display a Latitude / Longitude grid with labels over the map
        /// Would like to change the symbol properties for the lines and labels
        /// </summary>
        private void DisplayLatLongGrid()
        {


            //Create a new grid
            LatitudeLongitudeGrid gridLatLong = new Esri.ArcGISRuntime.UI.LatitudeLongitudeGrid();
            LatitudeLongitudeGrid gridLatLongDefault = new LatitudeLongitudeGrid();

            //Configure grid
            gridLatLong.LabelFormat = LatitudeLongitudeGridLabelFormat.DegreesMinutesSeconds;
            gridLatLong.LabelPosition = GridLabelPosition.TopRight;
            gridLatLong.LabelOffset = 50;
            if (localSetting.GetSettingValue(ApplicationLiterals.KeywordMapViewGrid) != null)
            {
                gridLatLong.IsVisible = (bool)localSetting.GetSettingValue(ApplicationLiterals.KeywordMapViewGrid);
            }
            else
            {
                gridLatLong.IsVisible = true;

            }
            gridLatLong.IsLabelVisible = true;

            try
            {
                myMapView.Grid = gridLatLong;
            }
            catch (Exception)
            {

                myMapView.Grid = gridLatLongDefault;
            }

            //myMapView.UpdateLayout();

        }

        private void UpdateGrid()
        {
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              if (myMapView.Grid != null && myMapView.Grid.LevelCount > 0)
            {
                //Get proper color
                Windows.UI.Color defaultColor = new Windows.UI.Color();
                System.Drawing.Color convertedDefaultColor = new System.Drawing.Color();
                if (Application.Current.Resources[resourceNameGridColor] != null)
                {
                    defaultColor = (Color)Application.Current.Resources[resourceNameGridColor];
                    convertedDefaultColor = System.Drawing.Color.FromArgb(defaultColor.A, defaultColor.R, defaultColor.G, defaultColor.B);

                }

                //Create line symbol for grid
                SimpleLineSymbol lineSym = new SimpleLineSymbol
                {
                    Color = convertedDefaultColor,
                    Style = SimpleLineSymbolStyle.Solid,
                    Width = 1
                };

                //Update for each level of grid
                for (int levels = 0; levels < myMapView.Grid.LevelCount; levels++)
                {

                    TextSymbol currentSymbol = (TextSymbol)myMapView.Grid.GetTextSymbol(levels);
                    currentSymbol.Color = convertedDefaultColor;
                    myMapView.Grid.SetTextSymbol(levels, (Esri.ArcGISRuntime.Symbology.Symbol)currentSymbol);

                    myMapView.Grid.SetLineSymbol(levels, (Esri.ArcGISRuntime.Symbology.Symbol)lineSym);

                }



            }
        }

        /// <summary>
        /// Will set the background grid of the map view
        /// NOTE: Since we can't add this code inside XAML, thanks to ESRI sdk, we need to force it here. 
        /// </summary>
        public void SetBackgroundGrid()
        {
            if (myMapView.BackgroundGrid.GridLineWidth != 0)
            {
                Services.SettingsServices.SettingsService _settings = Services.SettingsServices.SettingsService.Instance;

                CustomResource customColors = new CustomResource();
                customColors.InitializeComponent();
                if (customColors.ContainsKey("MapViewBackgroundGridLightColor"))
                {
                    var appT = _settings.AppTheme;
                    if (_settings.AppTheme == ApplicationTheme.Light)
                    {
                        Windows.UI.Color lightDefault = (Windows.UI.Color)customColors["MapViewBackgroundGridLightColor"];
                        myMapView.BackgroundGrid.Color = System.Drawing.Color.FromArgb(lightDefault.A, lightDefault.R, lightDefault.G, lightDefault.B);
                        //myMapView.BackgroundGrid.GridLineColor = (Windows.UI.Color)customColors["MapViewBackgroundGridDarkColor"];
                        //myMapView.BackgroundGrid.GridLineWidth = 5;

                    }
                    else
                    {
                        Windows.UI.Color darkDefault = (Windows.UI.Color)customColors["MapViewBackgroundGridDarkColor"];
                        myMapView.BackgroundGrid.Color = System.Drawing.Color.FromArgb(darkDefault.A, darkDefault.R, darkDefault.G, darkDefault.B);


                        //myMapView.BackgroundGrid.GridLineColor = (Windows.UI.Color)customColors["MapViewBackgroundGridLightColor"];
                        //myMapView.BackgroundGrid.GridLineWidth = 5;
                    }
                }
                myMapView.BackgroundGrid.GridLineWidth = 0;
                //myMapView.UpdateLayout();
            }

        }




        #endregion

        /// <summary>
        /// When user wants to load a data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MapPageAddMap_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //Refresh
            UpdateLayout();

            //Load
            try
            {
                await ViewModel.LoadingData();
            }
            catch (Exception)
            {

            }
        }

        public async void GPSMode_TappedAsync(object sender, TappedRoutedEventArgs e)
        {
            if (!ViewModel.userHasTurnedGPSOff)
            {
                ViewModel.userHasTurnedGPSOff = true;
                ViewModel.SetGPSModeIcon(Symbol.TouchPointer);

                if (ViewModel._geolocator != null)
                {
                    ViewModel._geolocator.StatusChanged -= ViewModel.Geolocal_StatusChangedAsync;
                    ViewModel._geolocator.PositionChanged -= ViewModel.OnPositionChanged;
                }

                ViewModel.ResetLocationGraphic();

                ViewModel.StopLocationRing();

            }
            else
            {

                ViewModel.StartLocationRing();

                if (ViewModel._geolocator != null)
                {
                    ViewModel._geolocator.StatusChanged += ViewModel.Geolocal_StatusChangedAsync;
                    ViewModel._geolocator.PositionChanged += ViewModel.OnPositionChanged;

                }
                else
                {
                    await ViewModel.SetGPS();
                }
                //await ViewModel.SetGPS();
                ViewModel.userHasTurnedGPSOff = false;

                ViewModel.SetGPSModeIcon();

            }
        }

        /// <summary>
        /// Event called when the opacity slider get its value changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpacitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // SPW 2019
            if (sender != null && mapsLoaded && myMapView.Map != null)
            {
                ////For layers
                //esriMap = myMapView.Map;
                //Slider senderSlider = sender as Slider;
                //string filename = senderSlider.Tag as string;
                //if (filename != null && esriMap.AllLayers.Count > 0)
                //{
                //    foreach (Layer ls in esriMap.AllLayers)
                //    {
                //        if (ls.Name.Contains(filename.Split('.')[0]))
                //        {
                //            ls.Opacity = senderSlider.Value / 100.0;
                //            break;
                //        }
                //    }

                //}

                ViewModel.SetLayerOpacity(sender as Slider);

            }
        }
        //Created By Jamel to allow user to zoom to the extent of the TPK file
        private void MapZoomExtentIcon_Tapped(object sender, TappedRoutedEventArgs e)
        {

            ViewModel.ZoomToLayer(true);
            //myMapView.SetViewpoint
                       
            
        }
    }
}
