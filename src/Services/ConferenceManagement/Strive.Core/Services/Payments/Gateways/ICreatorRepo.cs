using System.Threading.Tasks;

namespace Strive.Core.Services.Payments.Gateways
{
    public interface ICreatorRepo
    {
        Task<CreatorProfile?> FindByUserId(string userId);

        Task<CreatorProfile?> FindById(string creatorId);

        Task<CreatorProfile?> FindBySlug(string slug);

        /// <summary>
        ///     Stores a new profile or replaces the one with the same id. Throws <see cref="SlugTakenException" /> if another
        ///     creator has the slug (two requests at the same time cannot both get it).
        /// </summary>
        Task Save(CreatorProfile profile);
    }

    public class SlugTakenException : System.Exception
    {
        public SlugTakenException() : base("Another creator has this name.")
        {
        }
    }
}
