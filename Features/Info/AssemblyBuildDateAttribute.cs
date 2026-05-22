using System.Globalization;

namespace VerticalSliceProject.Features.Info;

/// <summary>
/// Stamps the assembly with the UTC build timestamp. Emitted by the csproj at compile time:
/// <code>
/// &lt;AssemblyAttribute Include="VerticalSliceProject.Features.Info.AssemblyBuildDateAttribute"&gt;
///   &lt;_Parameter1&gt;$([System.DateTime]::UtcNow.ToString("O"))&lt;/_Parameter1&gt;
/// &lt;/AssemblyAttribute&gt;
/// </code>
/// Read at runtime by <c>GetInfo</c> via <c>Assembly.GetEntryAssembly()?.GetCustomAttribute&lt;...&gt;()</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class AssemblyBuildDateAttribute : Attribute
{
    public AssemblyBuildDateAttribute(string buildDate)
    {
        this.BuildDate = DateTime.Parse(
            buildDate,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    public DateTime BuildDate { get; }
}
