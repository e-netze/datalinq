using E.DataLinq.Core.Services.Crypto;
using System.Collections.Generic;

namespace E.DataLinq.Core.Extensions;

static internal class CryptoServiceOptionsExtensions
{
    static public IEnumerable<CryptoServiceOptions> CurrentAndLegacy(this CryptoServiceOptions options)
    {
        yield return options;

        if (options.LegacyOptions != null)
        {
            foreach (var legacyOption in options.LegacyOptions)
            {
                yield return legacyOption;
            }
        }
    }
}
