using System.IO;



namespace Harmony.Data;



/// <summary>Centralizes file-system locations used by the app.</summary>

public static class AppPaths

{

    private const string NewFolderName = "RezinasMusic";

    private const string LegacyFolderName = "Harmony";



    /// <summary>Per-user data folder, e.g. %LOCALAPPDATA%\RezinasMusic.</summary>

    public static string DataFolder

    {

        get

        {

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var newDir = Path.Combine(local, NewFolderName);

            var legacyDir = Path.Combine(local, LegacyFolderName);



            if (!Directory.Exists(newDir) && Directory.Exists(legacyDir))

                return legacyDir;



            Directory.CreateDirectory(newDir);

            return newDir;

        }

    }



    /// <summary>Full path to the SQLite database file.</summary>

    public static string DatabaseFile => Path.Combine(DataFolder, "harmony.db");



    /// <summary>EF Core / SQLite connection string.</summary>

    public static string ConnectionString => $"Data Source={DatabaseFile}";



    /// <summary>Cache folder for downloaded artwork etc.</summary>

    public static string CacheFolder

    {

        get

        {

            var dir = Path.Combine(DataFolder, "cache");

            Directory.CreateDirectory(dir);

            return dir;

        }

    }



    /// <summary>Downloaded full-length audio streams (YouTube etc.).</summary>

    public static string StreamCacheFolder

    {

        get

        {

            var dir = Path.Combine(CacheFolder, "streams");

            Directory.CreateDirectory(dir);

            return dir;

        }

    }



    /// <summary>Imported / copied audio files from the user's PC.</summary>

    public static string LocalMusicFolder

    {

        get

        {

            var dir = Path.Combine(DataFolder, "music");

            Directory.CreateDirectory(dir);

            return dir;

        }

    }



    /// <summary>Diagnostic log files.</summary>

    public static string LogsFolder

    {

        get

        {

            var dir = Path.Combine(DataFolder, "logs");

            Directory.CreateDirectory(dir);

            return dir;

        }

    }

}

