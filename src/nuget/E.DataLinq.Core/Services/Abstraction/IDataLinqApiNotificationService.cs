using System.Threading.Tasks;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqApiNotificationService
{
    ValueTask ItemCreated(string id);
    ValueTask ItemUpdated(string id);
    ValueTask ItemDeleted(string id);
}
