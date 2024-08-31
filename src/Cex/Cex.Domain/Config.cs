using Cex.Domain.Base;

namespace Cex.Domain
{
    public class Config : BaseUpdatableEntity
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
