﻿using System;
using System.IO;
using UnityEngine;

namespace CustomAvatar.Avatar
{
    internal class AvatarInfo
    {
        /// <summary>
        /// Name of the avatar.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// Avatar author's name.
        /// </summary>
        public readonly string author;

        /// <summary>
        /// Avatar icon.
        /// </summary>
        public readonly Texture2D icon;

        /// <summary>
        /// File name of the avatar.
        /// </summary>
        public readonly string fileName;

        /// <summary>
        /// File size of the avatar.
        /// </summary>
        public readonly long fileSize;

        /// <summary>
        /// Date/time at which the avatar file was created.
        /// </summary>
        public readonly DateTime created;

        /// <summary>
        /// Date/time at which the avatar file was last modified.
        /// </summary>
        public readonly DateTime lastModified;

        /// <summary>
        /// Date/time at which this information was read from disk.
        /// </summary>
        public readonly DateTime timestamp;

        internal AvatarInfo(string name, string author, Texture2D icon, string fileName, long fileSize, DateTime created, DateTime lastModified, DateTime timestamp)
        {
            this.name = name;
            this.author = author;
            this.icon = icon;
            this.fileName = fileName;
            this.fileSize = fileSize;
            this.created = created;
            this.lastModified = lastModified;
            this.timestamp = timestamp;
        }

        public AvatarInfo(LoadedAvatar avatar)
        {
            name = avatar.descriptor.name;
            author = avatar.descriptor.author;
            icon = avatar.descriptor.cover ? avatar.descriptor.cover.texture : null;

            var fileInfo = new FileInfo(avatar.fullPath);

            fileName = fileInfo.Name;
            fileSize = fileInfo.Length;
            created = fileInfo.CreationTimeUtc;
            lastModified = fileInfo.LastWriteTimeUtc;

            timestamp = DateTime.Now;
        }

        public static bool operator ==(AvatarInfo left, AvatarInfo right)
        {
            if (ReferenceEquals(left, null)) return false;

            return left.Equals(right);
        }

        public static bool operator !=(AvatarInfo left, AvatarInfo right)
        {
            if (ReferenceEquals(left, null)) return false;

            return !left.Equals(right);
        }

        public bool IsForFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            var fileInfo = new FileInfo(filePath);

            return fileName == fileInfo.Name && fileSize == fileInfo.Length && created == fileInfo.CreationTimeUtc && lastModified == fileInfo.LastWriteTimeUtc;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is AvatarInfo other)) return false;

            return name == other.name && author == other.author && fileName == other.fileName && fileSize == other.fileSize && created == other.created && lastModified == other.lastModified && other.timestamp == timestamp;
        }

        public override int GetHashCode()
        {
            int hash = 23;

            var fields = new object[] { name, author, icon, fileName, fileSize, created, lastModified, timestamp };

            unchecked
            {
                foreach (object field in fields)
                {
                    if (field == null) continue;

                    hash = hash * 17 + field.GetHashCode();
                }
            }

            return hash;
        }
    }
}
