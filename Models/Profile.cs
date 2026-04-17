namespace PeekThrough.Models
{
    /// <summary>
    /// Opacity profile for Ghost Mode
    /// </summary>
    internal class Profile
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public byte Opacity { get; private set; }

        public Profile(string id, string name, byte opacity)
        {
            Id = id;
            Name = name;
            Opacity = opacity;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}/255)", Name, Opacity);
        }
    }
}
