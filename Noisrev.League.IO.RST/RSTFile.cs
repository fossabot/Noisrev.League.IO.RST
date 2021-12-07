﻿/*  This is a RST (Riot String Table) file class.
 *  
 *  The RST file is a League of Legends file used to store a list of strings.
 *  
 *  They are often used to store text content in different language versions so that League of Legends can reference and switch between different languages.
 *  
 *  The RST file is usually located in the "DATA/Menu" directory.
 *  
 *  Like: "DATA/Menu/fontconfig_en_us.txt", "DATA/Menu/bootstrap_zh_cn.stringtable".
 *  
 *  
 *  
 *  The file structure of the RST is as follows:
 *  
 *   
 *   ___________________________________________________________________________________________________________
 *   |     Pos:     |       0      |       3      |       4      |       8       |      ...     |      ...     |
 *   |--------------|--------------|--------------|--------------|---------------|--------------|--------------|
 *   |     Size:    |       3      |       1      |       4      |      8xN      |       1      |      ...     |
 *   |--------------|--------------|--------------|--------------|---------------|--------------|--------------|
 *   |    Format:   |    String    |     Byte     |     Int32    |     UInt64    |     Byte     |    Entries   |
 *   |--------------|--------------|--------------|--------------|---------------|--------------|--------------|
 *   | Description: |  Magic Code  |    Version   |     Count    | RST hash list |     Mode     |  Entry List  |
 *   |______________|______________|______________|______________|_______________|______________|______________|
 *
 *   *** "Mode" was deprecated in version 5 ***
 *   
 *  The entry structure:
 *                               ______________________________________________
 *                               |     Size:    |       ?      |       1      |
 *                               |--------------|--------------|--------------|
 *                               |    Format:   |    String    |     Byte     |
 *                               |--------------|--------------|--------------|
 *                               | Description: |    Content   |   End Byte   | // The end byte is always 0x00
 *                               |______________|______________|______________| // Like char* or char[] in C, always ending with 0x00 ('\0')
 *                               
 *                                                                                                       ---Author   : Noisrev(晚风✨)
 *                                                                                                       ---Email    : Noisrev@outlook.com
 *                                                                                                       ---DateTime : 7.2.2021 --13:14
 */

#pragma warning disable CS1591
using Noisrev.League.IO.RST.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Noisrev.League.IO.RST
{
    /// <summary>
    /// Riot String Table File
    /// </summary>
    public class RSTFile : IDisposable, IEquatable<RSTFile>
    {
        /// <summary>
        /// Loads a RST file from a file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The <see cref="RSTFile"/>.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="DecoderExceptionFallback"></exception>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public static RSTFile Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);
            
            return new RSTFile(File.OpenRead(filePath), false);
        }

        /// <summary>
        /// Magic Code
        /// </summary>
        public static string Magic = "RST";

        /// <summary>
        /// RST File Version
        /// </summary>
        public RVersion Version { get; }

        /// <summary>
        /// RST File Font Config, using by RST v2
        /// </summary>
        public string Config { get; private set; }

        /// <summary>
        /// The data segment is located at Position of the current stream
        /// </summary>
        public long DataOffset
        {
            get
            {
                /* Magic Code(3) + Version(1) */
                long offset = 4;

                // Version 2
                if (Version == RVersion.Ver2)
                {
                    /* hasConfig? (1) boolean */
                    offset += 1;

                    /* Config is not null ? */
                    if (!string.IsNullOrEmpty(Config) && Config.Length != 0)
                    {
                        /* size(int) + strlen */
                        offset += 4 + Config.Length;
                    }
                }

                /* count (4 bytes) +  8 * Count  ***/
                offset += 4 + (8 * _entries.Count);

                /* Version less than 5 */
                if (Version < RVersion.Ver5)
                {
                    offset += 1;
                }

                /* Return the offset */
                return offset;
            }
        }

        /// <summary>
        /// The type of RST used to generate the hash
        /// </summary>
        public RType Type { get; }

        /// <summary>
        /// Mode of the RST
        /// </summary>
        public RMode Mode { get; }

        /// <summary>
        /// Collection of RST entries
        /// </summary>
        public ReadOnlyCollection<RSTEntry> Entries { get; }

        /// <summary>
        /// Private list.
        /// </summary>
        private readonly List<RSTEntry> _entries;

        /// <summary>
        /// The stream that stores the RST data segment.
        /// </summary>
        private Stream _dataStream;

        /// <summary>
        /// Gets the RST entry by the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The <see cref="RSTEntry"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public RSTEntry this[string key] => Find(key);
        
        /// <summary>
        /// Gets the RST entry by the specified hash.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>The <see cref="RSTEntry"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public RSTEntry this[ulong hash] => Find(hash);


        /// <summary>
        /// Initialize the RSTFile class
        /// </summary>
        private RSTFile()
        {
            // Initialize entries
            this._entries = new List<RSTEntry>();
            // Set Entries to read-only
            this.Entries = _entries.AsReadOnly();
        }

        /// <summary>
        /// Initialize and set the version and Type
        /// </summary>
        /// <param name="version">RST Version</param>
        /// <exception cref="ArgumentException"></exception>
        public RSTFile(RVersion version) : this()
        {
            var type = version.GetRType();

            /* Check the type  */
            if (type == null) throw new ArgumentException($"Invalid Major version {(byte)version}. Must be one of 2, 3, 4, 5");

            this.Type = type.Value;
            this.Version = version;
        }

        /// <summary>
        /// Read the RST file from the stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="leaveOpen">true to leave the stream open after the <see cref="System.IO.BinaryReader"/> object is disposed; otherwise, false.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="DecoderExceptionFallback"></exception>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public RSTFile(Stream input, bool leaveOpen) : this()
        {
            // Init BinaryReader, use UTF-8
            using (BinaryReader br = new BinaryReader(input, Encoding.UTF8, leaveOpen))
            {
                // Read magic code
                var magic = br.ReadString(3);
                if (magic != Magic)
                {
                    // Invalid magic code
                    throw new InvalidDataException($"Invalid RST file header: {magic}");
                }

                //Set Version
                Version = (RVersion)br.ReadByte();

                // Version 2 and Version 3
                if (Version == RVersion.Ver2 || Version == RVersion.Ver3)
                {
                    // The keys for versions 2 and 3
                    Type = RType.Complex;
                    // Version 2
                    if (Version == RVersion.Ver2)
                    {
                        // 0 or 1
                        var hasConfig = br.ReadBoolean();
                        if (hasConfig) // true
                        {
                            // Config length
                            var length = br.ReadInt32();
                            // Read the Config.
                            Config = br.ReadString(length);
                        }
                    }
                    // Version 3
                    // pass
                }
                // If this is version 4 or version 5
                else if (Version == RVersion.Ver4 || Version == RVersion.Ver5)
                {
                    // Key for version 4 and 5
                    Type = RType.Simple;
                }
                // Not equivalent to versions 2, 3, 4, 5.
                else
                {
                    // Invalid or unsupported version and throws an exception.
                    throw new InvalidDataException($"Unsupported RST version: {Version}");
                }

                // Set hash key
                var hashKey = Type.ComputeKey();
                // Read Count
                var count = br.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    //Read the hash data
                    var hashGroup = br.ReadUInt64();

                    // Generate offset
                    var offset = Convert.ToInt64(hashGroup >> (int)Type);
                    // Generate hash
                    var hash = hashGroup & hashKey;

                    // Add entry
                    _entries.Add(new RSTEntry(offset, hash));
                }

                /* Version less than 5 */
                if (Version < RVersion.Ver5)
                {
                    // Read Mode
                    Mode = (RMode)br.ReadByte();
                }

                // Set Data Stream
                input.AutoCopy(out _dataStream);

                // Iterate through all the entries
                for (var i = 0; i < count; i++)
                {
                    // Set the content
                    ReadText(_entries[i]);
                }
            }
        }

        internal void CheckDuplicate(ulong hash)
        {
            // Check if the entry is already in the list
            if (_entries.Any(e => e.Hash == hash))
            {
                // Throw an exception
                throw new InvalidDataException($"Duplicate hash: {hash}");
            }
        }

        /// <summary>
        /// Add entry with key and value.
        /// </summary>
        /// <param name="key">The hash key</param>
        /// <param name="value">The content</param>
        public void AddEntry(string key, string value)
        {
            AddEntry(RSTHash.ComputeHash(key, Type), value);
        }

        /// <summary>
        /// Add entry with hash and value.
        /// </summary>
        /// <param name="hash">The hash</param>
        /// <param name="value">The content</param>
        public void AddEntry(ulong hash, string value)
        {
            CheckDuplicate(hash);
            _entries.Add(new RSTEntry(hash, value));
        }


        /// <summary>
        /// Add entry with <see cref="RSTEntry"/>.
        /// </summary>
        /// <param name="entry">The rst entry</param>
        public void AddEntry(RSTEntry entry)
        {
            CheckDuplicate(entry.Hash);
            _entries.Add(entry);
        }

        /// <summary>
        /// Find the entry that matches the hash.
        /// </summary>
        /// <param name="hash">The hash</param>
        /// <returns>If it does not exist in the list, return null.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public RSTEntry Find(ulong hash)
        {
            return _entries.Find(x => x.Hash == hash);
        }

        /// <summary>
        /// Find the entry that matches the key.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>If it does not exist in the list, return null.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public RSTEntry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            
            return Find(RSTHash.ComputeHash(key, Type));
        }

        /// <summary>
        /// Find the matching entry.
        /// </summary>
        /// <param name="match">The match function</param>
        /// <returns>If it does not exist in the list, return null.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public RSTEntry Find(Predicate<RSTEntry> match)
        {
            return _entries.Find(match);
        }

        /// <summary>
        /// Inserts an element into the <see cref="List{T}"/> at the specified index.
        /// </summary>
        /// <param name="index">The index</param>
        /// <param name="entry">The item</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public void Insert(int index, RSTEntry entry)
        {
            _entries.Insert(index, entry);
        }

        /// <summary>
        /// Remove all items that match hash.
        /// </summary>
        /// <param name="hash">The hash</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void Remove(ulong hash)
        {
            _entries.RemoveAll(x => x.Hash == hash);
        }

        /// <summary>
        /// Remove the entry.
        /// </summary>
        /// <param name="entry">The entry</param>
        /// <returns>true if item is successfully removed; otherwise, false. This method also returns false if item was not found in the <see cref="List{T}"/></returns>
        public bool Remove(RSTEntry entry)
        {
            return _entries.Remove(entry);
        }

        /// <summary>
        /// Removes the entry at the specified index
        /// </summary>
        /// <param name="index">The index</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void RemoveAt(int index)
        {
            _entries.RemoveAt(index);
        }

        /// <summary>
        /// Reading content begins at the offset specified in the stream.
        /// </summary>
        /// <param name="entry">Entry to be read</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="DecoderExceptionFallback"></exception>
        private void ReadText(RSTEntry entry)
        {
            // Set the text
            entry.Text = _dataStream.ReadStringWithEndByte(entry.Offset, 0x00);
        }

        /// <summary>
        /// Replace the matching items in the entire entry. And replace them.
        /// </summary>
        /// <param name="oldText">To Replace</param>
        /// <param name="newText">Replace to</param>
        /// <param name="caseSensitive">Case sensitive</param>
        /// <exception cref="ArgumentNullException"/>
        public void ReplaceAll(string oldText, string newText, bool caseSensitive = false)
        {
            // Set a list
            var list = caseSensitive
                ? _entries.Where(x => x.Text.Contains(oldText))
                : _entries.Where(x => x.Text.ToLower().Contains(oldText.ToLower()));

            foreach (var item in list)
            {
                // Set Text
                item.Text = newText;
            }
        }

        /// <summary>
        /// Set the configuration.
        /// </summary>
        /// <param name="conf">The config</param>
        /// <returns>It must be version 2.1 to set the configuration. Set to return true on success or false on failure.</returns>
        public bool SetConfig(string conf)
        {
            // Version 2
            if (Version == RVersion.Ver2)
            {
                // Set the config
                Config = conf;
                // Return
                return true;
            }
            // Not version 2
            else
            {
                // Return
                return false;
            }
        }

        /// <summary>
        /// Write the <see cref="RSTFile"/>.
        /// </summary>
        /// <param name="outputPath">The output path</param> 
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="EncoderFallbackException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ObjectDisposedException"/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="IOException"/>
        public void Write(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentNullException(nameof(outputPath));
            }
            
            using (var ms = new MemoryStream())
            {
                // Write to MemoryStream
                Write(ms, false);
                // Write All Bytes
                File.WriteAllBytes(outputPath, ms.ToArray());
            }
        }

        /// <summary>
        /// Using an output stream, write the RST to that stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="leaveOpen">true to leave the stream open after the  <see cref="System.IO.BinaryWriter"/> object is disposed; otherwise, false.</param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="EncoderFallbackException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ObjectDisposedException"/>
        /// <exception cref="OverflowException"/>
        /// <exception cref="IOException"/>
        public void Write(Stream output, bool leaveOpen)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            // Init Binary Writer
            using (BinaryWriter bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen))
            {
                // Write Magic Code
                bw.Write(Magic.ToCharArray());

                // Write Version
                bw.Write((byte)Version);

                // Version 2
                if (Version == RVersion.Ver2)
                {
                    var hasConfig = !string.IsNullOrEmpty(Config) && Config.Length != 0;
                    /* Config whether there is any content? */
                    bw.Write(hasConfig);

                    // True
                    if (hasConfig)
                    {
                        // Write Config
                        {
                            // Write Size
                            bw.Write(Config.Length);
                            // Write Content
                            bw.Write(Config.ToCharArray());
                        }
                    }
                }

                // Write Count
                bw.Write(_entries.Count);

                // Set the hash offset.
                var hashOffset = bw.BaseStream.Position;
                // Set the data offset.
                var dataOffset = hashOffset + (_entries.Count * 8) + (Version < RVersion.Ver5 ? 1 : 0); /* hashOffset + hashesSize + (byte)Mode */

                // Go to the dataOffset
                bw.BaseStream.Seek(dataOffset, SeekOrigin.Begin);

                // Initialize dictionary
                // Use a dictionary to filter duplicate items
                var offsets = new Dictionary<string, long>();

                // Write Data
                foreach (var entry in _entries)
                {
                    var text = entry.Text;

                    // If there is duplicate content in the dictionary.
                    if (offsets.ContainsKey(text))
                    {
                        // Set the offset. And do not write the content. Because there's repetition.
                        entry.Offset = offsets[text];
                    }
                    // No repeat
                    else
                    {
                        // Write Offset
                        entry.Offset = bw.BaseStream.Position - dataOffset;
                        // Write Text
                        bw.Write(Encoding.UTF8.GetBytes(text));
                        // Write End Byte
                        bw.Write((byte)0x00);

                        // Add to dictionary
                        offsets.Add(text, entry.Offset);
                    }
                }

                // Go to the hashOffset
                bw.BaseStream.Seek(hashOffset, SeekOrigin.Begin);
                // Write hashes
                foreach (var entry in _entries)
                {
                    // Write RST Hash
                    bw.Write(RSTHash.ComputeHash(entry.Hash, entry.Offset, Type));
                }

                /* Version less than 5 */
                if (Version < RVersion.Ver5)
                {
                    // Write Mode
                    bw.Write((byte)Mode);
                }
                // Flush to prevent unwritten data
                bw.Flush();

                // Dispose
                this.Dispose();
                // Set Data Stream
                output.AutoCopy(out _dataStream);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                (_dataStream as IDisposable)?.Dispose();
            }

            _dataStream = null;
        }
        public bool Equals(RSTFile other)
        {
            if (other == null)
            {
                return false;
            }

            if (!Version.Equals(other.Version))
            {
                return false;
            }

            if (Version == RVersion.Ver2 && !Config.Equals(other.Config))
            {
                return false;
            }

            if (Type != other.Type || Mode != other.Mode || _entries.Count != other._entries.Count)
            {
                return false;
            }

            var entries = _entries.OrderBy(x => x.Hash);
            var otherEntries = other._entries.OrderBy(x => x.Hash);

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!entries.ElementAt(i).Equals(otherEntries.ElementAt(i)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}