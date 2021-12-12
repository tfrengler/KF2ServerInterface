namespace KF2ServerInterface
{
    public sealed class KF2ServerInstance
    {
        public string Name { get; }
        public int Port { get; }
        public string DesiredMap { get; }
        public bool Down { get; set; }
        public bool Disabled { get; set; }

        public KF2ServerInstance(string name, int port, string desiredMap, bool disabled)
        {
            Name = name;
            Port = port;
            DesiredMap = desiredMap;
            Disabled = disabled;
        }
    }
}
