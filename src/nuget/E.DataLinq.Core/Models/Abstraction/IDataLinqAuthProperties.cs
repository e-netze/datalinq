namespace E.DataLinq.Core.Models.Abstraction;

public interface IDataLinqAuthProperties
{
    string Route { get; }
    string[] Access { get; set; }
    string[] AccessTokens { get; set; }
}
