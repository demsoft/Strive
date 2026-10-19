using System.Threading.Tasks;

namespace Strive.Core.Services.Payments.Gateways
{
    public interface ICreatorRepo
    {
        Task<CreatorProfile?> FindByUserId(string userId);

        Task<CreatorProfile?> FindById(string creatorId);

        /// <summary>Stores a new profile or replaces the one with the same id</summary>
        Task Save(CreatorProfile profile);
    }
}
