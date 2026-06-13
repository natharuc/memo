namespace Memo.Service.Classes
{
    public class Documento
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public Documento()
        {
        }

        public Documento(string key, string value)
        {
            Key = key?.Trim();
            Value = value?.Trim();
        }
    }
}
