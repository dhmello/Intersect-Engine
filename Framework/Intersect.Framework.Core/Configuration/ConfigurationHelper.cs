﻿using System.Text;

using Intersect.IO.Files;
using Intersect.Logging;

using Newtonsoft.Json;

namespace Intersect.Configuration;

public static partial class ConfigurationHelper
{
    public static string CacheName { get; set; } = default!;

    public static T Load<T>(T configuration, string filePath, bool failQuietly = false)
        where T : IConfiguration<T>
    {
        if (!File.Exists(filePath))
        {
            if (failQuietly)
            {
                return configuration;
            }

            throw new FileNotFoundException("Missing configuration file.", filePath);
        }

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);

            JsonConvert.PopulateObject(json, configuration);

            return configuration;
        }
        catch (Exception exception)
        {
            LegacyLogging.Logger?.Error(exception);

            throw;
        }
    }

    public static T Save<T>(T configuration, string filePath, bool failQuietly = false)
        where T : IConfiguration<T>
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath == null)
        {
            throw new ArgumentNullException();
        }

        if (!FileSystemHelper.EnsureDirectoryExists(directoryPath))
        {
            throw new FileNotFoundException("Missing directory.", directoryPath);
        }

        try
        {
            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);

            File.WriteAllText(filePath, json, Encoding.UTF8);

            return configuration;
        }
        catch (Exception exception)
        {
            LegacyLogging.Logger?.Error(exception);

            throw;
        }
    }

    public static T LoadSafely<T>(T configuration, string filePath = null)
        where T : IConfiguration<T>
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return configuration;
        }

        try
        {
            configuration.Load(filePath);
        }
        catch (Exception exception)
        {
            LegacyLogging.Logger?.Warn(exception);
        }
        finally
        {
            try
            {
                configuration.Save(filePath);
            }
            catch (Exception exception)
            {
                LegacyLogging.Logger?.Error(exception);
            }
        }

        return configuration;
    }
}
