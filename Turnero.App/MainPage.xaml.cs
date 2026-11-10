namespace Turnero.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
#if ANDROID
		SafeAreaEdges = new SafeAreaEdges(
			left: SafeAreaRegions.Container,
			top: SafeAreaRegions.None,
			right: SafeAreaRegions.Container,
			bottom: SafeAreaRegions.Container);
#endif
	}
}
