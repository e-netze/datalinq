using E.DataLinq.Core.Models.Authentication;

namespace E.DataLinq.Core.Services.Abstraction;

public interface IDataLinqCodeIdentityService
{
    DataLinqCodeIdentity CurrentIdentity();
}
