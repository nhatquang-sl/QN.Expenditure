namespace Cex.Application.BnbSetting.DTOs
{
    public class BnbSettingDto
    {
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }

        public static BnbSettingDto From(Domain.Entities.BnbSetting entity) => new()
        {
            ApiKey = entity.ApiKey,
            SecretKey = entity.SecretKey
        };
    }
}
