namespace E.DataLinq.Web.Extensions;

static class NumberExtensions
{
    static public string ToPlatformNumberString(this double value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
    }
}
