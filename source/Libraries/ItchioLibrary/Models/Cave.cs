﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.ComponentModel;

namespace ItchioLibrary.Models
{
    public enum GameType
    {
        /// <summary>
        /// GameTypeDefault is downloadable games
        /// </summary>
        @default,

        /// <summary>
        /// GameTypeFlash is for .swf (legacy)
        /// </summary>
        flash,

        /// <summary>
        /// GameTypeUnity is for .unity3d (legacy)
        /// </summary>
        unity,

        /// <summary>
        /// GameTypeJava is for .jar (legacy)
        /// </summary>
        java,

        /// <summary>
        /// GameTypeHTML is for .html (thriving)
        /// </summary>
        html
    }

    public enum GameClassification
    {
        /// <summary>
        /// GameClassificationGame is something you can play
        /// </summary>
        [Description("Games")]
        game,

        /// <summary>
        /// GameClassificationTool includes all software pretty much
        /// </summary>
        [Description("Tools")]
        tool,

        /// <summary>
        /// GameClassificationAssets includes assets: graphics, sounds, etc.
        /// </summary>
        [Description("Assets")]
        assets,

        /// <summary>
        /// GameClassificationGameMod are game mods (no link to game, purely creator tagging)
        /// </summary>
        [Description("Mods")]
        game_mod,

        /// <summary>
        /// GameClassificationPhysicalGame is for a printable / board / card game
        /// </summary>
        [Description("Physical games")]
        physical_game,

        /// <summary>
        /// GameClassificationSoundtrack is a bunch of music files
        /// </summary>
        [Description("Soundtracks")]
        soundtrack,

        /// <summary>
        /// GameClassificationOther is anything that creators think don’t fit in any other category
        /// </summary>
        [Description("Others")]
        other,

        /// <summary>
        /// GameClassificationComic is a comic book (pdf, jpg, specific comic formats, etc.)
        /// </summary>
        [Description("Comics")]
        comic,

        /// <summary>
        /// GameClassificationBook is a book (pdf, jpg, specific e-book formats, etc.)
        /// </summary>
        [Description("Books")]
        book
    }

    public class GameEmbedData
    {
        /// <summary>
        /// Game this embed info is for
        /// </summary>
        public long gameId;

        /// <summary>
        /// width of the initial viewport, in pixels
        /// </summary>
        public long width;

        /// <summary>
        /// height of the initial viewport, in pixels
        /// </summary>
        public long height;

        /// <summary>
        /// for itch.io website, whether or not a fullscreen button should be shown
        /// </summary>
        public bool fullscreen;
    }

    public enum Architectures
    {
        /// <summary>
        /// ArchitecturesAll represents any processor architecture
        /// </summary>
        [EnumMember(Value = "all")]
        all,

        ///Architectures386 represents 32-bit processor architectures
        [EnumMember(Value = "386")]
        x86,

        /// <summary>
        /// ArchitecturesAmd64 represents 64-bit processor architectures
        /// </summary>
        [EnumMember(Value = "amd64")]
        x64
    }

    public class Platforms
    {
        public Architectures? windows;
        public Architectures? linux;
        public Architectures? osx;
    }

    /// <summary>
    /// User represents an itch.io account, with basic profile info
    /// </summary>
    public class User
    {
        /// <summary>
        /// Site-wide unique identifier generated by itch.io
        /// </summary>
        public long id;

        /// <summary>
        /// The user’s username (used for login)
        /// </summary>
        public string username;

        /// <summary>
        /// The user’s display name: human-friendly, may contain spaces, unicode etc.
        /// </summary>
        public string displayName;

        /// <summary>
        /// Has the user opted into creating games?
        /// </summary>
        public bool developer;

        /// <summary>
        /// Is the user part of itch.io’s press program?
        /// </summary>
        public bool pressUser;

        /// <summary>
        /// The address of the user’s page on itch.io
        /// </summary>
        public string url;

        /// <summary>
        /// User’s avatar, may be a GIF
        /// </summary>
        public string coverUrl;

        /// <summary>
        /// Static version of user’s avatar, only set if the main cover URL is a GIF
        /// </summary>
        public string stillCoverUrl;
    }

    public class ItchioGame
    {
        /// <summary>
        /// Site-wide unique identifier generated by itch.io
        /// </summary>
        public long id;

        /// <summary>
        /// Canonical address of the game’s page on itch.io
        /// </summary>
        public string url;

        /// <summary>
        /// Human-friendly title (may contain any character)
        /// </summary>
        public string title;

        /// <summary>
        /// Human-friendly short description
        /// </summary>
        public string shortText;

        /// <summary>
        /// Downloadable game, html game, etc.
        /// </summary>
        public GameType type;

        /// <summary>
        /// Classification: game, tool, comic, etc.
        /// </summary>
        public GameClassification classification;

        /// <summary>
        /// optional Configuration for embedded(HTML5) games
        /// </summary>
        public GameEmbedData embed;

        /// <summary>
        /// Cover url(might be a GIF)
        /// </summary>
        public string coverUrl;

        /// <summary>
        /// Non-gif cover url, only set if main cover url is a GIF
        /// </summary>
        public string stillCoverUrl;

        /// <summary>
        /// Date the game was created
        /// </summary>
        public DateTime? createdAt;

        /// <summary>
        /// Date the game was published, empty if not currently published
        /// </summary>
        public DateTime? publishedAt;

        /// <summary>
        /// Price in cents of a dollar
        /// </summary>
        public long minPrice;

        /// <summary>
        /// Are payments accepted?
        /// </summary>
        public bool canBeBought;

        /// <summary>
        /// Does this game have a demo available?
        /// </summary>
        public bool hasDemo;

        /// <summary>
        /// Is this game part of the itch.io press system?
        /// </summary>
        public bool inPressSystem;

        /// <summary>
        /// Platforms this game is available for
        /// </summary>
        public Platforms platforms;

        /// <summary>
        /// optional The user account this game is associated to
        /// </summary>
        public User user;

        /// <summary>
        /// ID of the user account this game is associated to
        /// </summary>
        public long userId;

        public bool published;
    }

    /// <summary>
    /// UploadType describes what’s in an upload - an executable, a web game, some music, etc.
    /// </summary>
    public enum UploadType
    {
        /// <summary>
        /// UploadTypeDefault is for executables
        /// </summary>
        @default,

        /// <summary>
        /// UploadTypeFlash is for .swf files
        /// </summary>
        flash,

        /// <summary>
        /// UploadTypeUnity is for .unity3d files
        /// </summary>
        unity,

        /// <summary>
        /// UploadTypeJava is for .jar files
        /// </summary>
        java,

        /// <summary>
        /// UploadTypeHTML is for .html files
        /// </summary>
        html,

        /// <summary>
        /// UploadTypeSoundtrack is for archives with .mp3/.ogg/.flac/etc files
        /// </summary>
        soundtrack,

        /// <summary>
        /// ///
        /// </summary>
        book,

        /// <summary>
        /// UploadTypeVideo is for videos
        /// </summary>
        video,

        /// <summary>
        /// UploadTypeDocumentation is for documentation (pdf, maybe uhh doxygen?)
        /// </summary>
        documentation,

        /// <summary>
        /// UploadTypeMod is a bunch of loose files with no clear instructions how to apply them to a game
        /// </summary>
        mod,

        /// <summary>
        /// UploadTypeAudioAssets is a bunch of .ogg/.wav files
        /// </summary>
        audio_assets,

        /// <summary>
        /// UploadTypeGraphicalAssets is a bunch of .png/.svg/.gif files, maybe some .objs thrown in there
        /// </summary>
        graphical_assets,

        /// <summary>
        /// UploadTypeSourcecode is for source code. No further comments.
        /// </summary>
        sourcecode,

        /// <summary>
        /// UploadTypeOther is for literally anything that isn’t an existing category, or for stuff that isn’t tagged properly.
        /// </summary>
        other
    }

    /// <summary>
    /// An Upload is a downloadable file. Some are wharf-enabled, which means they’re actually a “channel” that may contain multiple builds, pushed with https://github.com/itchio/butler
    /// </summary>
    public class Upload
    {
        /// <summary>
        /// Site-wide unique identifier generated by itch.io
        /// </summary>
        public long id;

        /// <summary>
        /// Human-friendly name set by developer(example: Overland for Windows 64-bit)
        /// </summary>
        public string displayName;

        /// <summary>
        /// ID of the latest build for this upload, if it’s a wharf-enabled upload
        /// </summary>
        public long buildId;

        /// <summary>
        /// Upload type: default, soundtrack, etc.
        /// </summary>
        public UploadType type;
    }

    public class CaveStats
    {
        public DateTime? installedAt;
        public DateTime? lastTouchedAt;
        public long secondsRun;
    }

    public class CaveInstallInfo
    {
        public long installedSize;
        public string installLocation;
        public string installFolder;
        public bool pinned;
    }

    public class Cave
    {
        public string id;
        public ItchioGame game;
        public CaveStats stats;
        public CaveInstallInfo installInfo;
        public Upload upload;

        public override string ToString()
        {
            return game?.title ?? base.ToString();
        }
    }
}
