using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public abstract class HelixFileVersion
    {
        public abstract byte[] FileDesignator { get; }
        public abstract int DerivedBytesIterations { get; }

        public static HelixFileVersion Default = new Version01();
        public static HelixFileVersion UnitTest = new VersionUnitTest();
        public static HelixFileVersion[] AllVersions = new HelixFileVersion[]
        {
            UnitTest,
            Default,
        };

        class VersionUnitTest : HelixFileVersion
        {
            public override int DerivedBytesIterations { get; } = 2;
            public override byte[] FileDesignator { get; }
                = Encoding.ASCII.GetBytes("HxTest~0");
        }
        class Version01 : HelixFileVersion
        {
            public override int DerivedBytesIterations { get; } = 100000;
            public override byte[] FileDesignator { get; }
                = Encoding.ASCII.GetBytes("Helix~01");
        }

        public static HelixFileVersion GetVersion(byte[] fileDesignator)
        {
            if (fileDesignator == null)
                throw new ArgumentNullException(nameof(fileDesignator));

            return AllVersions
                .Where(ver => ByteBlock.Equals(ver.FileDesignator, fileDesignator))
                .FirstOrDefault();
        }
    }
}
