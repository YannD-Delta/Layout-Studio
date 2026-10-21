using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>An add-on the theme integrates with. Never required: each integration hides itself when its add-on is missing.</summary>
    public class Recommendation
    {
        public string Id;     // the add-on id of the add-on database, equal to the folder name of the installed extension
        public string Name;
        public string Why;
        public string WhyFr;
    }

    /// <summary>The optional add-ons worth installing next to Modula, and what is still missing on this machine.</summary>
    public static class Recommendations
    {
        private static readonly Recommendation[] Desktop =
        {
            new Recommendation { Id = "ExtraMetadataLoader_705fdbca-e1fc-4004-b839-1d040b8b4429", Name = "Extra Metadata Loader", Why = "logos and videos on the game page", WhyFr = "logos et vidéos sur la page du jeu" },
            new Recommendation { Id = "PlayniteAchievements", Name = "Playnite Achievements", Why = "achievements section and card", WhyFr = "section et carte des succès" },
            new Recommendation { Id = "playnite-howlongtobeat-plugin", Name = "HowLongToBeat", Why = "time-to-beat card and section", WhyFr = "carte et section du temps pour finir" },
            new Recommendation { Id = "playnite-gameactivity-plugin", Name = "GameActivity", Why = "activity card and chart", WhyFr = "carte et graphique d’activité" },
            new Recommendation { Id = "felixkmh_Extras_Plugin", Name = "ThemeExtras", Why = "extra elements for themes", WhyFr = "éléments supplémentaires pour les thèmes" },
            new Recommendation { Id = "UniPlaySong.a1b2c3d4-e5f6-7890-abcd-ef1234567890", Name = "UniPlaySong", Why = "each game plays its own music while you browse", WhyFr = "chaque jeu joue sa propre musique pendant la navigation" },
            new Recommendation { Id = "CustomFadeAnim_155b27bc-6c8c-47dc-ae41-e74568b2fe9f", Name = "Custom Fade Animations", Why = "smooth background transitions when the game changes", WhyFr = "transitions de fond fluides quand on change de jeu" },
            new Recommendation { Id = "ImageRotater.72b7d457-0621-429b-8368-665bc53ff896", Name = "ImageRotater", Why = "rotating, animated and video covers and backgrounds", WhyFr = "jaquettes et fonds qui tournent, animés ou vidéo" },
            new Recommendation { Id = "NowPlaying_db4e7ade-57fb-426c-8392-60e2347a0209", Name = "Now Playing", Why = "a top bar chip to return to the running game", WhyFr = "une pastille de la barre du haut pour revenir au jeu en cours" }
        };

        private static readonly Recommendation[] Fullscreen =
        {
            new Recommendation { Id = "ExtraMetadataLoader_705fdbca-e1fc-4004-b839-1d040b8b4429", Name = "Extra Metadata Loader", Why = "logos and videos", WhyFr = "logos et vidéos" },
            new Recommendation { Id = "UniPlaySong.a1b2c3d4-e5f6-7890-abcd-ef1234567890", Name = "UniPlaySong", Why = "each game plays its own music on the home screen", WhyFr = "chaque jeu joue sa propre musique sur l’écran d’accueil" },
            // add-ons with a maintained release in the last 18 months whose data the Fullscreen theme shows (each integration hides itself when the add-on is missing)
            new Recommendation { Id = "NowPlaying_db4e7ade-57fb-426c-8392-60e2347a0209", Name = "Now Playing", Why = "a chip and a Back to game button while a game runs", WhyFr = "une pastille et un bouton Retour au jeu pendant qu’un jeu tourne" },
            new Recommendation { Id = "ControllerSessionManager_6f3e7a21-98f4-4f2b-92ad-3fc0e6e941dc", Name = "Controller Session Manager", Why = "controller battery in the top bar", WhyFr = "batterie de la manette dans la barre du haut" },
            new Recommendation { Id = "PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1", Name = "Audio Switcher", Why = "volume in the top bar", WhyFr = "volume dans la barre du haut" },
            new Recommendation { Id = "MoData_5a0a672a-998a-4cf3-94ea-44543de5afcb", Name = "MoData", Why = "Wi-Fi strength in the top bar", WhyFr = "force du Wi-Fi dans la barre du haut" },
        };

        public static IList<Recommendation> For(bool isDesktop)
        {
            return isDesktop ? Desktop : Fullscreen;
        }

        public static IList<Recommendation> Missing(IEnumerable<string> installedIds, bool isDesktop)
        {
            var installed = new HashSet<string>((installedIds ?? new string[0]).Where(i => i != null), StringComparer.OrdinalIgnoreCase);
            return For(isDesktop).Where(r => !installed.Contains(r.Id)).ToList();
        }

        public static string Message(IList<Recommendation> missing, bool french = false)
        {
            if (missing == null || missing.Count == 0) return "";
            if (french)
                return "Modula fonctionne sans eux (chaque intégration se masque quand son add-on manque), mais ces add-ons optionnels le mettent en valeur : "
                    + string.Join(" ; ", missing.Select(r => r.Name + " (" + (r.WhyFr ?? r.Why) + ")")) + ".";
            return "Modula works without them (each integration hides itself when its add-on is missing), but these optional add-ons make it shine: "
                + string.Join("; ", missing.Select(r => r.Name + " (" + r.Why + ")")) + ".";
        }

        /// <summary>Identifies a list of missing add-ons, so the same list is not announced twice.</summary>
        public static string Signature(IList<Recommendation> missing)
        {
            return string.Join("|", missing.Select(r => r.Id).OrderBy(i => i, StringComparer.Ordinal));
        }
    }
}
