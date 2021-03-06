using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using Producer.Auth;
using Producer.Domain;
using Producer.Droid.Services;
using Producer.Shared;
using System.Threading.Tasks;

using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace Producer.Droid
{
	[Activity (Label = "Producer", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : BaseActivity
	{
		TabFragmentPagerAdapter PagerAdapter;
		IMenu menu;


		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			Bootstrap.Run ();

			//read in extra keys/values from the incoming intent
			if (Intent.Extras != null)
			{
				foreach (var key in Intent.Extras.KeySet ())
				{
					var value = Intent.Extras.GetString (key);
					Log.Debug ($"Key: {key} Value: {value}");
				}
			}

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);
			var toolbar = FindViewById<Toolbar> (Resource.Id.main_toolbar);

			//Toolbar will now take on default Action Bar characteristics
			SetSupportActionBar (toolbar);

			setupViewPager ();

			ClientAuthManager.Shared.AuthorizationChanged += handleClientAuthChanged;
			ProducerClient.Shared.CurrentUserChanged += handleCurrentUserChanged;

			RegisterForNotifications ();
		}


		protected override async void OnResume ()
		{
			base.OnResume ();

			checkCompose ();

			if (!await Settings.IsConfigured ())
			{
				ShowConfigAlert ();
			}
		}


		protected override void Dispose (bool disposing)
		{
			if (disposing)
			{
				ClientAuthManager.Shared.AuthorizationChanged -= handleClientAuthChanged;
				ProducerClient.Shared.CurrentUserChanged -= handleCurrentUserChanged;
			}

			base.Dispose (disposing);
		}


		void handleClientAuthChanged (object sender, ClientAuthDetails authDetails)
		{
			Log.Debug ($"Authenticated: {authDetails}");

			Task.Run (async () =>
			{
				if (authDetails == null)
				{
					ProducerClient.Shared.ResetUser ();
				}
				else
				{
					await ProducerClient.Shared.AuthenticateUser (authDetails.Token, authDetails.AuthCode);
				}

				await ContentClient.Shared.GetAllAvContent ();
			});
		}


		void handleCurrentUserChanged (object sender, User e)
		{
			Log.Debug ($"User: {e?.ToString ()}");

			checkCompose ();

			RegisterForNotifications ();
		}


		void checkCompose ()
		{
			// Check if signed-in user has write access
			if (menu != null)
			{
				RunOnUiThread (() =>
				{
					var composeItem = menu.FindItem (Resource.Id.action_compose);
					composeItem?.SetVisible (ProducerClient.Shared.UserRole.CanWrite ());
				});
			}
		}


		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			switch (item.ItemId)
			{
				case Resource.Id.action_profile:
					profileButtonClicked ();
					return true;
				case Resource.Id.action_settings:
					StartActivity (typeof (SettingsActivity));
					return true;
				case Resource.Id.action_compose:
					return true;
				case Android.Resource.Id.Home:

					return false;
			}

			return base.OnOptionsItemSelected (item);
		}


		public override bool OnPrepareOptionsMenu (IMenu menu)
		{
			checkCompose ();

			return base.OnPrepareOptionsMenu (menu);
		}


		public override bool OnCreateOptionsMenu (IMenu menu)
		{
			this.menu = menu;
			MenuInflater.Inflate (Resource.Menu.menu_main, menu);

			return base.OnCreateOptionsMenu (menu);
		}


		public void ShowConfigAlert (string alertTitle = "Configure App", string alertMessage = "Enter the \"Site Name\" used when deploying the to Azure:")
		{
			Settings.BeginConfig ();

			var view = LayoutInflater.FromContext (this).Inflate (Resource.Layout.EditTextDialog, FindViewById<ViewGroup> (Android.Resource.Id.Content), false);
			var textView = view.FindViewById<AutoCompleteTextView> (Resource.Id.input);
			textView.Hint = Resources.GetString (Resource.String.site_name);

			var builder = new AlertDialog.Builder (this)
				 .SetTitle (alertTitle)
				 .SetMessage (alertMessage)
				 .SetView (view)
				 .SetCancelable (false)
				 .SetPositiveButton ("OK", async (sender, e) =>
				 {
					 var text = textView.Text;

					 if (string.IsNullOrEmpty (text))
					 {
						 ShowConfigAlert ();
					 }
					 else
					 {
						 Settings.AzureSiteName = text;

						 await ProducerClient.Shared.UpdateAppSettings ();

						 Settings.CompleteConfig ();

						 RegisterForNotifications ();
					 }
				 })
				 .Show ();
		}


		void RegisterForNotifications ()
		{
			if (this.CheckPlayServicesAvailable ())
			{
				// Start RegistrationIntentService to register this application with Azure Notification Hub.
				StartService (new Intent (this, typeof (RegistrationIntentService)));
			}
		}


		void setupViewPager ()
		{
			PagerAdapter = new TabFragmentPagerAdapter (this, SupportFragmentManager);
			PagerAdapter.AddFragment (new ContentRecyclerFragment (), false);
			PagerAdapter.AddFragment (new FavoritesRecyclerFragment (), false);

			var viewPager = FindViewById<ViewPager> (Resource.Id.main_viewPager);
			viewPager.Adapter = PagerAdapter;

			var tabLayout = FindViewById<TabLayout> (Resource.Id.main_tabLayout);
			tabLayout.TabMode = TabLayout.ModeFixed;
			tabLayout.TabGravity = TabLayout.GravityFill;
			tabLayout.SetupWithViewPager (viewPager);

			PagerAdapter.FillTabLayout (tabLayout);
			SupportActionBar.Title = PagerAdapter.GetTabFragment (0).Title;

			viewPager.PageSelected += (sender, e) =>
			{
				////update the query listener
				//var fragment = PagerAdapter.GetFragmentAtPosition (e.Position);
				//queryListener = (SearchView.IOnQueryTextListener)fragment;

				//searchView?.SetOnQueryTextListener (queryListener);

				//swap the title into the app bar title rather than including it in the tab
				var tabFragment = PagerAdapter.GetTabFragment (e.Position);
				SupportActionBar.Title = tabFragment.Title;
			};
		}


		void profileButtonClicked ()
		{
			ClientAuthManager.Shared.AuthActivityLayoutResId = Resource.Layout.Login;
			ClientAuthManager.Shared.GoogleWebClientResId = Resource.String.default_web_client_id;
			ClientAuthManager.Shared.GoogleButtonResId = Resource.Id.sign_in_button;

			if (ProducerClient.Shared.User == null)
			{
				StartActivity (typeof (LoginActivity));
			}
			else
			{
				StartActivity (typeof (UserActivity));
			}
		}
	}
}