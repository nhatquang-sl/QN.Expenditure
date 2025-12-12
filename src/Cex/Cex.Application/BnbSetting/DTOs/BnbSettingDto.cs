using Cex.Application.Common.Mappings;

namespace Cex.Application.BnbSetting.DTOs
{
    public class BnbSettingDto : IMapFrom<Domain.Entities.BnbSetting>
    {
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
    }
}