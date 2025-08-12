using System.Collections.Generic;
using System.Threading.Tasks;
using Dxs.Bsv.Models;

namespace Dxs.Bsv.Factories;

public interface IUtxoSetProvider
{
    Task<IList<OutPoint>> GetUtxoSet(Address address, TokenId tokenId = null);
}