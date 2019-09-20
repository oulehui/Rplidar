﻿using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Penny
{
    /// <summary>
    /// 讯飞的离线语音合成
    /// 所需要的tts：common/xiaofeng/xiaoyan文件存放于StreamingAssets/tts/路径下，GF打包时候会自动清空StreamingAssets目录
    /// 如果遇到'src'警告，尝试Reimport
    /// </summary>
    public class XunFeiTTSComponent : GameFrameworkComponent
    {
        [SerializeField]
        [Header("替换APPID请把讯飞的SDK重新复制到Plugins")]
        private string m_AppID = "appid = 5c64e955, work_dir = .";
        [SerializeField]
        private string m_UserName = string.Empty;
        [SerializeField]
        private string m_UserPassword = string.Empty;
        [SerializeField]
        private AudioSource m_AudioSource = null;

        private XunFeiTemplate m_XunFeiTemplate = null;

        protected void Start()
        {

            m_XunFeiTemplate = new XunFeiTemplate(
                m_UserName == string.Empty ? null : m_UserName,
                m_UserPassword == string.Empty ? null : m_UserPassword,
                m_AppID);
            //m_XunFeiTemplate.MultiSpeek("太年轻了，还没有活成自己喜欢的样子，也遇不到可以共度余生的人。虽然青春的最珍贵之处在于无知，在不懂爱的年纪学会如何喜欢一个人。但是其实爱情的最高境界在于不将就。可是好多人都会飞蛾扑火啊，就算没有结果，也还是愿意去浪费这个时间，这份感情。其实等我们足够优秀了，那个人会出现的吧。何必为了一个不确定的人放弃自己的未来呢？不过偶尔冲动一下也是必要的吧。", "Call12.wav");
            //MultiSpeak("该用 户不存在 你家二大爷");
        }

        /// <summary>
        /// 设置发音人
        /// </summary>
        /// <param name="speeker"></param>
        public void SetSpeaker(Speeker speeker)
        {
            m_XunFeiTemplate.SetSpeaker(speeker);
        }

        protected void OnDestroy()
        {
            m_XunFeiTemplate.CloseXunFei();
            m_XunFeiTemplate = null;
        }

        public float MultiSpeak(string SpeekText)
        {
            return Multi_Speak(SpeekText, Speeker.小燕_青年女声_中英文_普通话);
        }

        public float Multi_Speak(string SpeekText, Speeker speeker)
        {
            SetSpeaker(speeker);
            MemoryStream memoryStream = m_XunFeiTemplate.SpeechSynthesis(SpeekText);
            AudioClip audioClip = ToAudioClip(memoryStream.ToArray());
            if (audioClip == null)
            {
                return -1;
            }
            m_AudioSource.Stop();
            m_AudioSource.PlayOneShot(audioClip);
            return audioClip.length;
        }

        public void SaveMultiSpeak(string SpeekText, string filename)
        {
            StartCoroutine(Save(SpeekText, filename));
        }
        private IEnumerator Save(string SpeekText, string filename)
        {
            SetSpeaker(Speeker.小燕_青年女声_中英文_普通话);
            MemoryStream memoryStream = m_XunFeiTemplate.SpeechSynthesis(SpeekText);
            yield return memoryStream;
            FileStream ofs = new FileStream(filename + ".wav", FileMode.Create);
            memoryStream.WriteTo(ofs);
            ofs.Close();
            ofs = null;
            AudioClip audioClip = ToAudioClip(memoryStream.ToArray());
            if (audioClip == null)
            {
                yield break;
            }
            m_AudioSource.Stop();
            m_AudioSource.PlayOneShot(audioClip);
        }

        public static AudioClip ToAudioClip(byte[] fileBytes, int offsetSamples = 0, string name = "ifly")
        {
            //string riff = Encoding.ASCII.GetString (fileBytes, 0, 4);
            //string wave = Encoding.ASCII.GetString (fileBytes, 8, 4);
            int subchunk1 = BitConverter.ToInt32(fileBytes, 16);
            //ushort audioFormat = BitConverter.ToUInt16(fileBytes, 20);

            // NB: Only uncompressed PCM wav files are supported.
            //string formatCode = FormatCode(audioFormat);
            //Debug.AssertFormat(audioFormat == 1 || audioFormat == 65534, "Detected format code '{0}' {1}, but only PCM and WaveFormatExtensable uncompressed formats are currently supported.", audioFormat, formatCode);

            ushort channels = BitConverter.ToUInt16(fileBytes, 22);
            int sampleRate = BitConverter.ToInt32(fileBytes, 24);
            //int byteRate = BitConverter.ToInt32 (fileBytes, 28);
            //UInt16 blockAlign = BitConverter.ToUInt16 (fileBytes, 32);
            ushort bitDepth = BitConverter.ToUInt16(fileBytes, 34);

            int headerOffset = 16 + 4 + subchunk1 + 4;
            int subchunk2 = BitConverter.ToInt32(fileBytes, headerOffset);
            //Debug.LogFormat ("riff={0} wave={1} subchunk1={2} format={3} channels={4} sampleRate={5} byteRate={6} blockAlign={7} bitDepth={8} headerOffset={9} subchunk2={10} filesize={11}", riff, wave, subchunk1, formatCode, channels, sampleRate, byteRate, blockAlign, bitDepth, headerOffset, subchunk2, fileBytes.Length);

            //Log.Info(bitDepth);

            float[] data;
            switch (bitDepth)
            {
                case 8:
                    data = Convert8BitByteArrayToAudioClipData(fileBytes, headerOffset, subchunk2);
                    break;
                case 16:
                    data = Convert16BitByteArrayToAudioClipData(fileBytes, headerOffset, subchunk2);
                    break;
                case 24:
                    data = Convert24BitByteArrayToAudioClipData(fileBytes, headerOffset, subchunk2);
                    break;
                case 32:
                    data = Convert32BitByteArrayToAudioClipData(fileBytes, headerOffset, subchunk2);
                    break;
                default:
                    throw new Exception(bitDepth + " bit depth is not supported.");
            }

            AudioClip audioClip = AudioClip.Create(name, data.Length, channels, sampleRate, false);
            audioClip.SetData(data, 0);
            return audioClip;
        }

        private static string FormatCode(UInt16 code)
        {
            switch (code)
            {
                case 1:
                    return "PCM";
                case 2:
                    return "ADPCM";
                case 3:
                    return "IEEE";
                case 7:
                    return "μ-law";
                case 65534:
                    return "WaveFormatExtensable";
                default:
                    Debug.LogWarning("Unknown wav code format:" + code);
                    return "";
            }
        }

        #region wav file bytes to Unity AudioClip conversion methods

        private static float[] Convert8BitByteArrayToAudioClipData(byte[] source, int headerOffset, int dataSize)
        {
            int wavSize = BitConverter.ToInt32(source, headerOffset);
            headerOffset += sizeof(int);
            Debug.AssertFormat(wavSize > 0 && wavSize == dataSize, "Failed to get valid 8-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

            float[] data = new float[wavSize];

            sbyte maxValue = sbyte.MaxValue;

            int i = 0;
            while (i < wavSize)
            {
                data[i] = (float)source[i] / maxValue;
                ++i;
            }

            return data;
        }

        private static float[] Convert16BitByteArrayToAudioClipData(byte[] source, int headerOffset, int dataSize)
        {
            int wavSize = BitConverter.ToInt32(source, headerOffset);
            headerOffset += sizeof(int);
            Debug.AssertFormat(wavSize > 0 && wavSize == dataSize, "Failed to get valid 16-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

            int x = sizeof(Int16); // block size = 2
            int convertedSize = wavSize / x;

            float[] data = new float[convertedSize];

            Int16 maxValue = Int16.MaxValue;

            int offset = 0;
            int i = 0;
            while (i < convertedSize)
            {
                offset = i * x + headerOffset;
                data[i] = (float)BitConverter.ToInt16(source, offset) / maxValue;
                ++i;
            }

            Debug.AssertFormat(data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

            return data;
        }

        private static float[] Convert24BitByteArrayToAudioClipData(byte[] source, int headerOffset, int dataSize)
        {
            int wavSize = BitConverter.ToInt32(source, headerOffset);
            headerOffset += sizeof(int);
            Debug.AssertFormat(wavSize > 0 && wavSize == dataSize, "Failed to get valid 24-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

            int x = 3; // block size = 3
            int convertedSize = wavSize / x;

            int maxValue = Int32.MaxValue;

            float[] data = new float[convertedSize];

            byte[] block = new byte[sizeof(int)]; // using a 4 byte block for copying 3 bytes, then copy bytes with 1 offset

            int offset = 0;
            int i = 0;
            while (i < convertedSize)
            {
                offset = i * x + headerOffset;
                Buffer.BlockCopy(source, offset, block, 1, x);
                data[i] = (float)BitConverter.ToInt32(block, 0) / maxValue;
                ++i;
            }

            Debug.AssertFormat(data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

            return data;
        }

        private static float[] Convert32BitByteArrayToAudioClipData(byte[] source, int headerOffset, int dataSize)
        {
            int wavSize = BitConverter.ToInt32(source, headerOffset);
            headerOffset += sizeof(int);
            Debug.AssertFormat(wavSize > 0 && wavSize == dataSize, "Failed to get valid 32-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

            int x = sizeof(float); //  block size = 4
            int convertedSize = wavSize / x;

            Int32 maxValue = Int32.MaxValue;

            float[] data = new float[convertedSize];

            int offset = 0;
            int i = 0;
            while (i < convertedSize)
            {
                offset = i * x + headerOffset;
                data[i] = (float)BitConverter.ToInt32(source, offset) / maxValue;
                ++i;
            }

            Debug.AssertFormat(data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

            return data;
        }

        #endregion
    }

}