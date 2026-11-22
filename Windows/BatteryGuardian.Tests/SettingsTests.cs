using System;
using System.Linq;
using System.Text.Json;
using BatteryGuardian.SmartPlug;
using Xunit;

namespace BatteryGuardian.Tests
{
 /// <summary>
 /// Settings is copied by the settings dialog and round-tripped through JSON on
 /// every save. Both used to lose fields without a word - the dialog rebuilt the
 /// object property by property and had never heard of the smart-plug settings,
 /// so opening Settings and pressing Save reset them. These tests fail the
 /// moment a new property is added and either path forgets it.
 /// </summary>
 public class SettingsTests
 {
  [Fact]
  public void CloneCopiesEveryWritableProperty()
  {
   Settings original = Populate();
   Settings copy = original.Clone();

   AssertEveryPropertyMatches(original, copy);
  }

  [Fact]
  public void JsonRoundTripPreservesEveryWritableProperty()
  {
   Settings original = Populate();

   string json = JsonSerializer.Serialize(original);
   Settings? restored = JsonSerializer.Deserialize<Settings>(json);

   Assert.NotNull(restored);
   AssertEveryPropertyMatches(original, restored!);
  }

  [Fact]
  public void DefaultsAreUsable()
  {
   var settings = new Settings();

   Assert.True(settings.LowBatteryThreshold < settings.HighBatteryThreshold);
   Assert.True(settings.AlertRepeatIntervalSeconds > 0);
   Assert.True(settings.IdealZoneLowPercent < settings.IdealZoneHighPercent);
   Assert.InRange(settings.SmartPlugPort, 1, 65535);
   Assert.InRange(settings.SmartPlugRelayDp, 1, 255);

   // The plug levels must satisfy the same rule the controller enforces, or the
   // feature is dead on arrival for anyone who enables it without editing them.
   // One pair of levels drives both the alerts and the plug.
   Assert.True(settings.LowBatteryThreshold < settings.HighBatteryThreshold);
   Assert.True(settings.HighBatteryThreshold - settings.LowBatteryThreshold
     >= SmartPlugConfig.MinimumGapPercent);
  }

  private static void AssertEveryPropertyMatches(Settings expected, Settings actual)
  {
   var properties = typeof(Settings).GetProperties().Where(p => p.CanWrite).ToArray();
   Assert.NotEmpty(properties);

   foreach (var property in properties)
   {
    Assert.True(
      Equals(property.GetValue(expected), property.GetValue(actual)),
      $"{property.Name} was not preserved (expected {property.GetValue(expected)}, got {property.GetValue(actual)})");
   }
  }

  /// <summary>
  /// Fills every writable property with a distinctive value, so a field that is
  /// dropped stands out rather than matching its default by luck.
  /// </summary>
  private static Settings Populate()
  {
   var settings = new Settings();

   foreach (var property in typeof(Settings).GetProperties().Where(p => p.CanWrite))
   {
    property.SetValue(settings, SampleValue(property.PropertyType, property.Name));
   }

   return settings;
  }

  private static object SampleValue(Type type, string name)
  {
   if (type == typeof(int)) return 47;
   if (type == typeof(bool)) return true;
   if (type == typeof(string)) return name + "-set";
   if (type == typeof(DateTime?)) return new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

   // A new property type means this helper needs a case, and the test says so
   // rather than passing quietly.
   throw new InvalidOperationException(
     $"Add a sample value for {type.Name} to keep the copy and JSON tests meaningful.");
  }
 }
}
