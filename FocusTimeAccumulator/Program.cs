﻿using System.Threading;
using FocusTimeAccumulator;
using FocusTimeAccumulator.Features.Bucket;
using FocusTimeAccumulator.Features.Pool;
using FocusTimeAccumulator.Features.Similarity;

class Program
{
	public static string appSettingfile = "settings.json";
	public static Settings settings;
	public static bool exiting = false;
	public static string prevName = "";
	public static string prevTitle = "";

	static FocusPool pool = new FocusPool( );
	static FocusBucket bucket = new FocusBucket( );

	static System.Timers.Timer timer = new System.Timers.Timer( );
	public static async Task Main( )
	{
		//if the settings exist
		if ( File.Exists( appSettingfile ) )
			settings = SaveData.DeserializeJson<Settings>( appSettingfile );
		else
		{
			settings = new( ); //this was exposed, but i don't need it to be anymore
			settings.appSettings.Add( new( ) );
			SaveData.SerializeJson( appSettingfile, settings ); //save settings immediately
		}
		//set up timer to tick at the timespan set in the settings
		timer.Elapsed += ( _, _ ) => Tick( );
		timer.Interval = settings.tickTime.TotalMilliseconds;

		Console.CancelKeyPress += ( _, e ) =>
		{
			e.Cancel = true;
			exiting = true;
			Console.WriteLine( "ctrl+c was pressed, saving..." );
			var thread = new Thread( TickThread );
			threads.Add( thread );
			thread.IsBackground = false;
		
			thread.Start( );
		};
		//start timer and then hold the program open indefinitely
		timer.Start( );
		await Task.Delay( -1 );
		timer.Stop( );
		//timer.Dispose( );
	}

	static List<Thread> threads = new List<Thread>();


	[STAThread]
	static void Tick( )
	{
		var thread = new Thread( TickThread );
		threads.Add( thread );
		thread.IsBackground = false;
		thread.Start( );
	}

	public static void TickThread( ) {

		//better foreground process finding suggestion from issue #2
		var appProcess = FocusFinder.WindowsProcessFocusApi.GetForegroundProcess( );
		var appName = appProcess?.ProcessName ?? "Unknown";
		var appTitle = appProcess?.MainWindowTitle ?? "Unknown";
		var lastInput = FocusFinder.WindowsProcessFocusApi.GetLastInputTime( );

		//if we did not get an input device ping in more than (settings.idleTime), add [Idle] tag to process
		if ( settings.idleModeEnabled && ( DateTime.Now - lastInput ) > settings.idleTime )
			appTitle = appTitle.Insert( 0, "[Idle] " );

		//limit app titles based on character length
		if ( appTitle.Length > settings.maxTitleLength )
			appTitle = appTitle[ ..settings.maxTitleLength ];

		//if the process name or title changes
		if ( exiting || ( prevTitle != appTitle || prevName != appName) )
		{
			//check the flags to see if the user wants to run both pool and or buckets at the same time
			if ( settings.focusSetting.HasFlag( Settings.FocusSetting.pool ) )
				pool.DoFocusPool( prevName, prevTitle );

			if ( settings.focusSetting.HasFlag( Settings.FocusSetting.bucket ) )
				bucket.DoFocusBucket( prevName, prevTitle );

			//set old name and title
			prevName = appName;
			prevTitle = appTitle;
			Thread.Sleep( 3000 );
			if ( exiting )
			{
				Console.WriteLine( $"saved you can now safely close the terminal" );
				Environment.Exit( 0 );
			}
		}

	}

}