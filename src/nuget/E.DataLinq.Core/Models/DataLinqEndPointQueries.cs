namespace E.DataLinq.Core.Models;

public class DataLinqEndPointQueries
{
    public string EndPointId { get; set; }
    public string QueryGroup { get; set; }
    public DataLinqEndPointQuery[] Queries { get; set; }
}
