namespace Lib.ExternalServices.KuCoin.Models
{
    public class KuCoinResponse<T>
    {
        public string Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }
    }
}