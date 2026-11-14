using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Sound (AUDIO-LIST.md): plays clips from Resources/Audio/<id> (.ogg/.wav/.mp3); <id>_1, <id>_2 … are variants picked at random.
    // A missing clip is silence, never an error, so the sounds can arrive one file at a time. Music crossfades between loops; a sting
    // (victory, defeat) plays once over a faded loop; an ambience bed loops under everything. Volumes live in the save.
    public class Sfx : MonoBehaviour
    {
        static Sfx I;
        static readonly Dictionary<string, AudioClip[]> clips = new();
        static readonly Dictionary<string, float> lastAt = new();
        public static readonly List<string> Asked = new();   // tests: every sound the game asked for, in order (found or not)
        public static bool Logging;
        AudioSource[] pool; int next; AudioSource musicA, musicB, amb, sting; string musicId, ambId; float fade = 1;
        const int Voices = 12; const float MusicFade = 1.2f;

        static Main.Save Save => Main.G ? Main.G.save : null;
        public static float MusicVol => Save != null ? Save.musicVol : .6f;
        public static float SfxVol => Save != null ? Save.sfxVol : .8f;

        static Sfx Get()
        {
            if (I) return I;
            var go = new GameObject("Sfx"); DontDestroyOnLoad(go); I = go.AddComponent<Sfx>();
            I.pool = new AudioSource[Voices]; for (int i = 0; i < Voices; i++) { I.pool[i] = go.AddComponent<AudioSource>(); I.pool[i].playOnAwake = false; }
            AudioSource Loop() { var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = true; return s; }
            I.musicA = Loop(); I.musicB = Loop(); I.amb = Loop(); I.sting = go.AddComponent<AudioSource>(); I.sting.playOnAwake = false;
            return I;
        }
        static AudioClip[] Clips(string id)
        {
            if (clips.TryGetValue(id, out var c)) return c;
            var list = new List<AudioClip>();
            foreach (var dir in Folders)
            {   // the first folder that has the sound (one file, or its _1 _2 … variants) wins
                var one = Resources.Load<AudioClip>("Audio/" + dir + id); if (one) list.Add(one);
                for (int i = 1; i <= 8; i++) { var v = Resources.Load<AudioClip>($"Audio/{dir}{id}_{i}"); if (v) list.Add(v); else if (i > 1) break; }
                if (list.Count > 0) break;
            }
            return clips[id] = list.ToArray();
        }
        // where the files may sit under Resources/Audio: loose, or in the owner's folders (music, moba, combat, announcer: in Thai or English)
        static readonly string[] Folders = { "", "music/", "moba/", "ui/", "combat/", "การต่อสู้/", "announcer/", "เสียงประกาศ/", "ambience/", "บรรยากาศ/" };
        public static bool Has(string id) => Clips(id).Length > 0;
        // a sound with no file of its own borrows a close one until it has one (owner's files, 2026-11-14: these were not made yet)
        static readonly Dictionary<string, string> Alias = new()
        {
            ["ui_open"] = "ui_select", ["ui_close"] = "ui_click", ["ui_dialogue"] = "ui_click", ["ui_reward"] = "coin", ["ui_mvp"] = "ui_levelup", ["ui_error"] = "block",
            ["boss_roar"] = "guardian", ["potion"] = "heal", ["hurt"] = "hurt_m",
        };
        static AudioClip Pick(string id)
        {
            var c = Clips(id); if (c.Length == 0 && Alias.TryGetValue(id, out var other)) c = Clips(other);
            return c.Length == 0 ? null : c[Random.Range(0, c.Length)];
        }

        // a sound effect; the same one again within 'gap' seconds is dropped (a crowd of hits stays one sound)
        public static void Play(string id, float vol = 1, float pitchVar = .06f, float gap = .05f)
        {
            if (string.IsNullOrEmpty(id)) return; if (Logging) Asked.Add(id);
            float now = Time.unscaledTime; if (lastAt.TryGetValue(id, out var t) && now - t < gap) return; lastAt[id] = now;
            var clip = Pick(id); if (!clip) return; var s = Get(); var src = s.pool[s.next++ % Voices];
            src.pitch = 1 + Random.Range(-pitchVar, pitchVar); src.PlayOneShot(clip, vol * SfxVol);
        }
        // a sound in the world: quieter the further it is from the camera's target, none past 32 m
        public static void At(string id, Vector3 pos, float vol = 1, float gap = .05f)
        {
            var G = Main.G; if (G && G.hero) { float d = Vector3.Distance(Main.Flat(pos), Main.Flat(G.hero.Pos)); if (d > 32) return; vol *= Mathf.Clamp01(1 - (d - 10) / 22); }
            Play(id, vol, .08f, gap);
        }
        // the music loop (null: none); the same id again keeps playing
        public static void Music(string id)
        {
            if (Logging) Asked.Add("music:" + (id ?? "-"));
            var s = Get(); if (s.musicId == id) return; s.musicId = id;
            var clip = id != null ? Pick(id) : null; (s.musicA, s.musicB) = (s.musicB, s.musicA);   // A is the new one, B fades out
            s.musicA.clip = clip; s.musicA.volume = 0; if (clip) s.musicA.Play(); else s.musicA.Stop(); s.fade = 0;
        }
        // a short piece over the music (victory, defeat): the loop dips while it plays
        public static void Sting(string id)
        {
            if (Logging) Asked.Add("sting:" + id);
            var clip = Pick(id); var s = Get(); Music(null); if (!clip) return; s.sting.clip = clip; s.sting.volume = MusicVol; s.sting.Play();
        }
        public static void Ambience(string id)
        {
            if (Logging) Asked.Add("amb:" + (id ?? "-"));
            var s = Get(); if (s.ambId == id) return; s.ambId = id; var clip = id != null ? Pick(id) : null;
            s.amb.clip = clip; if (clip) { s.amb.volume = SfxVol * .35f; s.amb.Play(); } else s.amb.Stop();
        }
        public static bool Speak(string id) { Play(id); return true; }   // a voice line inside a condition (always true)
        public static void Volumes() { if (!I) return; I.amb.volume = SfxVol * .35f; I.sting.volume = MusicVol; }
        void Update()
        {
            fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / MusicFade); float mv = MusicVol;
            musicA.volume = fade * mv; musicB.volume = (1 - fade) * mv; if (fade >= 1 && musicB.isPlaying) musicB.Stop();
        }
        // what each skill sounds like when it goes off
        public static string SkillSound(string skill) => skill switch
        {
            "whirl" or "gale" => "whirl", "slam" or "dive" => "slam", "cry" => "cry", "iaido" or "dance" or "petal" => "dash", "feather" => "bow_shot",
            "laser" or "missile" => "magic_cast", "shield" => "shield_up", "heal" => "heal", _ => "magic_cast",
        };
        // the music and ambience of a story stage, by its theme
        public static string AmbienceOf(string theme) => theme switch { "forest" or "valley" => "amb_forest", "river" => "amb_river", "castle" => "amb_castle", "kingdom" => "amb_town", _ => null };
    }
}
