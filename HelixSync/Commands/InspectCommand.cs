// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class InspectCommand
    {
        public static int Inspect(InspectOptions options, ConsoleEx consoleEx = null)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            consoleEx = consoleEx ?? new ConsoleEx();

            using (Stream fileIn = File.OpenRead(options.File))
            using (HelixFileDecryptor decryptor = new HelixFileDecryptor(fileIn))
            {
                consoleEx.WriteLine("== Outer Header ==");
                Dictionary<string, byte[]> header = null;
                decryptor.Initialize(DerivedBytesProvider.FromPassword(options.Password, options.KeyFile), (h) => header = h);
                consoleEx.WriteLine("Designator:    "
                                  + BitConverter.ToString(header[nameof(FileHeader.fileDesignator)]).Replace("-", "")
                                  + " (" + new string(Encoding.ASCII.GetString(header[nameof(FileHeader.fileDesignator)])
                                                      .Select(c => Char.IsControl(c) ? '?' : c)
                                                      .ToArray()) + ")");
                consoleEx.WriteLine("Password Salt: " + BitConverter.ToString(header[nameof(FileHeader.passwordSalt)]).Replace("-", ""));
                consoleEx.WriteLine("HMAC Salt:     " + BitConverter.ToString(header[nameof(FileHeader.hmacSalt)]).Replace("-", ""));
                consoleEx.WriteLine("IV:            " + BitConverter.ToString(header[nameof(FileHeader.iv)]).Replace("-", ""));
                consoleEx.WriteLine("Authn (HMAC):  " + BitConverter.ToString(header[nameof(FileHeader.headerAuthnDisk)]).Replace("-",""));
                consoleEx.WriteLine();

                consoleEx.WriteLine("== Inner Header ==");
                decryptor.ReadHeader(
                    afterRawMetadata: (r) => consoleEx.WriteLine(JsonFormat(r)));
                consoleEx.WriteLine();

                using (Stream content = decryptor.GetContentStream())
                {
                    ContentPreview(content, consoleEx);
                    consoleEx.WriteLine();
                }
            }

            //Reopen to calculate the checksum
            using (Stream fileIn = File.OpenRead(options.File))
            using (HelixFileDecryptor decryptor = new HelixFileDecryptor(fileIn))
            {
                decryptor.Initialize(DerivedBytesProvider.FromPassword(options.Password, options.KeyFile));
                decryptor.ReadHeader();
                using (Stream content = decryptor.GetContentStream())
                {
                    Checksum(content, consoleEx);
                }
            }

            return 0;
        }

        /// <summary>
        /// Calculates the checksum (with progress) and writes it to the console
        /// </summary>
        private static void Checksum(Stream stream, ConsoleEx consoleEx)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentOutOfRangeException(nameof(stream), "stream must have CanRead set to true");

            consoleEx.WriteLine("== Content Checksum (MD5) ==");
            using (MD5 md5 = MD5.Create())
            {
                consoleEx.Write("[    ] Calculating MD5...".PadRight(40) + new string('\b', 40));
                Action<double> onProgressChange = p => consoleEx.Write($"[{p:0,4:0%}]" + new string('\b', 6));
                var checksum = md5.ComputeHash(new ProgressStream(stream, onProgressChange));
                consoleEx.Write(new string(' ', 40) + new string('\b', 40)); //Clears the line
                consoleEx.WriteLine("MD5: " + BitConverter.ToString(checksum).Replace("-", ""));
            }
        }

        /// <summary>
        /// Allows tracking the progress of a stream (based of it's position)
        /// </summary>
        private class ProgressStream : Stream
        {
            private readonly Stream m_baseStream;
            private readonly Action<double> m_OnProgressChange;

            public ProgressStream(Stream baseStream, Action<double> onProgressChange)
            {
                if (baseStream == null)
                    throw new ArgumentNullException(nameof(baseStream));
                if (onProgressChange == null)
                    throw new ArgumentNullException(nameof(onProgressChange));
                this.m_baseStream = baseStream;
                this.m_OnProgressChange = onProgressChange;
            }

            public override bool CanRead { get { return m_baseStream.CanRead; } }

            public override bool CanSeek { get { return m_baseStream.CanSeek; } }

            public override bool CanWrite { get { return m_baseStream.CanWrite; } }

            public override long Length { get { return m_baseStream.Length; } }

            public override long Position
            {
                get { return m_baseStream.Position; }
                set
                {
                    m_baseStream.Position = value;
                    OnProgressChange();
                }
            }

            public override void Flush() { m_baseStream.Flush(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int result = m_baseStream.Read(buffer, offset, count);
                OnProgressChange();
                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long result = m_baseStream.Seek(offset, origin);
                OnProgressChange();
                return result;

            }

            public override void SetLength(long value)
            {
                m_baseStream.SetLength(value);
                OnProgressChange();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                m_baseStream.Write(buffer, offset, count);
                OnProgressChange();
            }

            private void OnProgressChange()
            {
                double progress = (double)Position / Length;
                m_OnProgressChange(progress);
            }
        }
        
        /// <summary>
        /// Displays a preview of the content (to the console), automatically determining the best viewer.
        /// </summary>
        /// <param name="stream">The stream for which to load the preview data</param>
        /// <param name="type">(option) used to override the automatic detected viewer</param>
        /// <param name="targetLength">(option) used to override the default length of preview</param>
        private static void ContentPreview(Stream stream, ConsoleEx consoleEx, string type = "auto", int targetLength = -1)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("steam must be readable", nameof(stream));

            const int defaultLength = 512;
            const int defaultBinaryLength = 256;

            int offset = 0;
            byte[] content = new byte[targetLength <= 0 ? defaultLength : targetLength];
            int length = stream.Read(content, 0, content.Length);

            string display;

            if (string.IsNullOrEmpty(type) || type == "auto")
                type = DetectType(content, offset, length);

            //Reduces size by two for binary due to the fact that it takes a lot of screen realistate
            if (type == "binary" && targetLength < 0)
                length = defaultBinaryLength;


            bool partial = length < stream.Length;
            if (length < stream.Length)
            {
                display = "== Content Preview (" + type + " " + FormatSizeBytes(length) + " of " + FormatSizeBytes(stream.Length) + ") ==" + Environment.NewLine;
            }
            else
                display = "== Content Preview (" + type + " " + FormatSizeBytes(stream.Length) + ") ==" + Environment.NewLine;

            if (type == "text")
            {
                display += TextDisplay(content, offset, length);
                if (partial)
                    display += "...";
            }
            else if (type == "json")
            {
                display += JsonDisplay(content, offset, length);
                if (partial)
                    display += "...";
            }
            else
            {
                display += HexDisplay(content, offset, length);

            }

            consoleEx.WriteLine(display);
        }

        /// <summary>
        /// Uses an abbreviated form of the size in units of KB, MB or GB
        /// </summary>
        private static string FormatSizeBytes(long bytes)
        {
            //from https://stackoverflow.com/questions/1242266/converting-bytes-to-gb-in-c#2082893
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return String.Format("{0:0.##}{1}", dblSByte, Suffix[i]);
        }

        /// <summary>
        /// Attempts to detect the type of content based off the provided data (up to 1KB)
        /// </summary>
        /// <returns>binary, text or json</returns>
        private static string DetectType(byte[] content, int offset, int length)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "offset must be >= 0");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
            if (offset + length > content.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "offset+length must not exceed the content size");

            string detectedType = "binary";

            string headerBytes = BitConverter.ToString(content, 0 + offset, length < 4 ? length : 4).Replace("-", " ");
            //see https://en.wikipedia.org/wiki/Byte_order_mark
            if (headerBytes.StartsWith("EF BB BF") ||
                headerBytes.StartsWith("FE FF") ||
                headerBytes.StartsWith("FF FE") ||
                headerBytes.StartsWith("00 00 FE FF") ||
                headerBytes.StartsWith("FF FE 00 00") ||
                headerBytes.StartsWith("2B 2F 76 38") ||
                headerBytes.StartsWith("2B 2F 76 39") ||
                headerBytes.StartsWith("2B 2F 76 2B") ||
                headerBytes.StartsWith("2B 2F 76 2F") ||
                headerBytes.StartsWith("F7 64 4C") ||
                headerBytes.StartsWith("DD 73 66 73") ||
                headerBytes.StartsWith("0E FE FF") ||
                headerBytes.StartsWith("FB EE 28") ||
                headerBytes.StartsWith("84 31 95 33")
                )
            {
                //matches byte order marks, most likely text
                detectedType = "text";
            }
            else
            {
                //does fuzzy matching
                byte[] c1 = new byte[length > 1024 ? 1024 : length];
                Array.Copy(content, c1, c1.Length);

                //if the text contains any character less then 8 (control characters) 
                //then in is unlikely text and most likely binary
                if (c1.Any(b => b <= 8))
                    detectedType = "binary";
                else
                    detectedType = "text";
            }

            if (detectedType == "text")
            {
                //test for more specific type then just text
                using (var mem = new MemoryStream(content, offset, (length > 1024 ? 1024 : length)))
                using (var text = new StreamReader(mem))
                {
                    var textContent = text.ReadToEnd();

                    //matches '{' or '[' followed by whitespace then '{','[','"', '0'-'9'
                    if (System.Text.RegularExpressions.Regex.IsMatch(textContent, @"^[\{\[][\s\r\n\t]*[\{\[\""0-9]", System.Text.RegularExpressions.RegexOptions.Multiline))
                    {
                        detectedType = "json";
                    }
                }
            }

            return detectedType;
        }

        private static string HexDisplay(byte[] content, int offset, int length)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "offset must be >= 0");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
            if (offset + length > content.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "offset+length must not exceed the content size");

            StringBuilder output = new StringBuilder();
            output.AppendLine("Offset     0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");

            int end = offset + length;

            while (offset < end)
            {
                int blockLength = 16;
                if (end - offset < 16)
                    blockLength = end - offset;

                var offsetStr = offset.ToString("X").PadLeft(8, '0');

                string hex = BitConverter.ToString(content, 0 + offset, blockLength);
                hex = hex.Replace('-', ' ') + new string(' ', (16 - blockLength) * 3);
                hex = hex.Substring(0, 8 * 3) + ' ' + hex.Substring(8 * 3);

                string text = "";

                for (int i = 0; i < blockLength; i++)
                {
                    byte b = content[i + offset];
                    var c = (char)b;
                    if (b == 0)
                        c = '␀';
                    else if (c == '\t')
                        c = '␉';
                    else if (char.IsControl(c))
                        c = '␦';
                    text += c;
                }

                text = text.Substring(0, 8) + " " + text.Substring(8);

                output.AppendLine(offsetStr + "   " + hex + "   " + text);
                offset += 16;
            }

            return output.ToString();
        }
        public static string TextDisplay(byte[] content, int offset, int length)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "offset must be >= 0");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
            if (offset + length > content.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "offset+length must not exceed the content size");


            using (var mem = new MemoryStream(content, offset, (length > 1024 ? 1024 : length)))
            using (var text = new StreamReader(mem))
            {
                var textContent = text.ReadToEnd();
                return textContent;
            }
        }
        public static string JsonDisplay(byte[] content, int offset, int length)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "offset must be >= 0");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
            if (offset + length > content.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "offset+length must not exceed the content size");

            using (var mem = new MemoryStream(content, offset, (length > 1024 ? 1024 : length)))
            using (var text = new StreamReader(mem))
            {
                var textContent = text.ReadToEnd();
                return JsonFormat(textContent);
            }
        }
        public static string JsonFormat(string str)
        {
            //Adapted from https://stackoverflow.com/questions/4580397/json-formatter-in-c#6237866
            //
            //Changes: Combined to single function & remove existing whitespace

            const string INDENT_STRING = "  ";
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();
            for (var i = 0; i < str.Length; i++)
            {
                var ch = str[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            foreach (var item in Enumerable.Range(0, ++indent))
                                sb.Append(INDENT_STRING);
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            foreach (var item in Enumerable.Range(0, --indent))
                                sb.Append(INDENT_STRING);
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && str[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            foreach (var item in Enumerable.Range(0, indent))
                                sb.Append(INDENT_STRING);
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;

                    case '\t':
                    case ' ':
                    case '\n':
                    case '\r':
                        if (quoted)
                            sb.Append(ch);
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
