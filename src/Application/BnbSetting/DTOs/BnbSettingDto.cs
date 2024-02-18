using Application.Common.Mappings;

namespace Application.BnbSetting.DTOs
{
    public class BnbSettingDto : IMapFrom<Domain.Entities.BnbSetting>
    {
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
    }
}
