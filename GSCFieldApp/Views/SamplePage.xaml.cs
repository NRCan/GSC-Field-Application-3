using GSCFieldApp.Services;
using GSCFieldApp.ViewModel;
using System;

namespace GSCFieldApp.Views;

public partial class SamplePage : ContentPage
{
    public LocalizationResourceManager LocalizationResourceManager
        => LocalizationResourceManager.Instance; // Will be used for in code dynamic local strings

    public SamplePage(SampleViewModel vm)
	{
        InitializeComponent();
        BindingContext = vm;

        this.Loaded += SamplePage_Loaded;
    }

    private async void SamplePage_Loaded(object sender, EventArgs e)
    {
        //Conditional visual remainder for surficial to take duplicate or blank sample
        SampleViewModel vm3 = this.BindingContext as SampleViewModel;
        bool needReminder = await vm3.DuplicateReminder();

        if (needReminder)
        {
            await Shell.Current.DisplayAlert(LocalizationResourceManager["SamplePageDuplicateReminderTitle"].ToString(),
                    LocalizationResourceManager["SamplePageDuplicateReminderMessage"].ToString(),
                    LocalizationResourceManager["GenericButtonOk"].ToString());
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        //After binding context is setup fill pickers
        SampleViewModel vm2 = this.BindingContext as SampleViewModel;
        await vm2.FillPickers();
        await vm2.InitModel();
        await vm2.Load(); //In case it is coming from an existing record in field notes

    }

    /// <summary>
    /// Special validation for paleomagnetism
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SamplePurposeCollectionControl_SizeChanged(object sender, EventArgs e)
    {
        //Validate paleomag controls visibility
        SampleViewModel vm4 = this.BindingContext as SampleViewModel;
        vm4.ValidateForPaleomagnetism();
    }

    /// <summary>
    /// Special validation for paleomagnetism
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SamplePagePurposePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        //Validate paleomag controls visibility
        SampleViewModel vm5 = this.BindingContext as SampleViewModel;
        vm5.ValidateForPaleomagnetism();
    }

    /// <summary>
    /// Special validation for paleomagnetism
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SampleTypePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        //Validate paleomag controls visibility
        SampleViewModel vm6 = this.BindingContext as SampleViewModel;
        vm6.ValidateForPaleomagnetism();
    }
}