namespace Turnero.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
#if ANDROID
		SafeAreaEdges = new SafeAreaEdges(
			left: SafeAreaRegions.None,
			top: SafeAreaRegions.None,
			right: SafeAreaRegions.None,
			bottom: SafeAreaRegions.Container);
#endif
	}
}
