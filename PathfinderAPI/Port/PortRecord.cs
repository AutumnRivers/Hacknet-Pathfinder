using System;
using Hacknet;

namespace Pathfinder.Port
{
    public class PortRecord : IEquatable<PortRecord>
    {
        public string Protocol { get; }
        public int OriginalPortNumber { get; }
        public string DefaultDisplayName { get; }
        public int DefaultPortNumber { get; private set; }
        public PortRecord(string protocol, string defDisplayName, int defPortNumber) : this(protocol, defDisplayName, defPortNumber, defPortNumber)
        {
        }

        internal PortRecord(string protocol, string defDisplayName, int defPortNumber, int originalPortNumber)
        {
            if(protocol == null)
                throw new ArgumentNullException(nameof(protocol));

            Protocol = protocol;
            DefaultDisplayName = defDisplayName;
            DefaultPortNumber = defPortNumber;
            OriginalPortNumber = originalPortNumber;
        }

        public PortState CreateState(Computer computer, string displayName = null, int portNumber = -1, bool cracked = false)
        {
            return new PortState(computer, this, displayName, portNumber, cracked);
        }

        public bool Equals(PortRecord other)
        {
            return Protocol == other.Protocol && OriginalPortNumber == other.OriginalPortNumber;
        }
        public static bool operator ==(PortRecord first, PortRecord second)
        {
            return first.Equals(second);
        }
        public static bool operator !=(PortRecord first, PortRecord second)
        {
            return !(first == second);
        }
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((PortRecord)obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Protocol != null ? Protocol.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ OriginalPortNumber;
                return hashCode;
            }
        }

        [Obsolete("Avoid PortData")]
        public static explicit operator PortRecord(PortData data)
            => new PortRecord(data.Protocol, data.DisplayName, data.Port, data.OriginalPort);

        [Obsolete("Avoid PortData")]
        public static explicit operator PortData(PortRecord record)
            => new PortData(record.Protocol, record.OriginalPortNumber, record.DefaultPortNumber, record.DefaultDisplayName);
    }
}