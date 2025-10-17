namespace DataLinq.Test.DataLinqWeb.HelperTests;

internal class RawContentTestable
{
    public RawContentTestable(object value)
    {
        Value = value;
    }

    public object Value { get; }

    public override string ToString() => Value?.ToString() ?? "";

    public override bool Equals(object? obj)
    {
        return obj is RawContentTestable other &&
               other.ToString() == this.ToString();
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}

