﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Generic;
using Helpers;

namespace StreamingKit.Media.TS
{

    public class HikPSAnalyze : PSAnalyze
    {
    }

    public partial class PSAnalyze
    {

        private readonly object _lock = new object();
        private IOMemoryStream ms = new IOMemoryStream();
        private Boolean _firstAudioFrame = true;

        private readonly List<PESPacket> _listAudioPES = new List<PESPacket>();
        private readonly List<PESPacket> _listVideoPES = new List<PESPacket>();
        private readonly AQueue<MediaFrame> _queueFrame = new AQueue<MediaFrame>();
        private Thread _analyzeThead = null;
        private bool _isWorking = false;

        public int PID;
        public bool IsVideo;
        public bool IsAudio;
        public byte[] SPS;
        public byte[] PPS;
        public int Width;
        public int Height;
        public int Frequency;
        public int Channel;
        public short AudioFormat;
        public short Samples;
        public int VideoFrameCount;
        public int AudioFrameCount;

        public void Start()
        {
            if (_isWorking)
                return;
            _isWorking = true;
            _analyzeThead = ThreadEx.ThreadCall(AnalyzeThead);
        }

        public void Stop()
        {
            if (!_isWorking)
                return;
            _isWorking = false;
            ThreadEx.Stop(_analyzeThead);
            _listAudioPES.Clear();
            _listVideoPES.Clear();
            _queueFrame.Clear();
            ms.Close();
        }

        public List<MediaFrame> ReadFrames()
        {
            if (!_isWorking)
                return new List<MediaFrame>();

            var result = new List<MediaFrame>();
            while (_queueFrame.Count > 0)
            {
                var item = _queueFrame.Dequeue();
                result.Add(item);
            }
            return result;
        }

        public void Write(byte[] psdata)
        {
            if (!_isWorking)
                return;
            while (ms.Length - ms.ReadPosition > 1024 * 1024)
            {
                Thread.Sleep(10);
            }
            lock (_lock)
            {
                ms.Write(psdata, 0, psdata.Length);
            }
        }

        private void AnalyzeThead()
        {
            long scr = 0;
            while (_isWorking)
            {
                bool findStartCode = false;
                byte flag = 0;
                lock (_lock)
                {
                    if (ms.Length - ms.ReadPosition > 4)
                    {
                        findStartCode = ms.ReadByte() == 0x00 && ms.ReadByte() == 0x00 && ms.ReadByte() == 0x01;
                        if (findStartCode)
                        {
                            flag = (byte)ms.ReadByte();
                            if (flag == 0xBA || flag == 0xBB || flag == 0xBC || flag == 0xE0 || flag == 0xC0)
                            {
                                ms.Seek(-4, SeekOrigin.Current);
                            }
                        }
                    }
                }
                if (findStartCode)
                {
                    try
                    {
                        switch (flag)
                        {
                            case 0xBA://PS流包头
                                var ph = new PSPacketHeader(ms);
                                scr = ph.GetSCR();
                                break;
                            case 0xBB://PS流系统头,有些流没有该值
                                new PSSystemHeader(ms);
                                break;
                            case 0xBC://PS流map
                                var map = new PSMap(ms);
                                break;
                            case 0xE0://PES包(视频)
                                var pesVideo = new PESPacket();
                                pesVideo.SetBytes(ms);
                                var frame = AnalyzeNewFrame(pesVideo, false);
                                if (frame != null)
                                    frame.NTimetick = scr;
                                if (frame != null && frame.IsKeyFrame == 1)
                                {
                                    lock (_lock)
                                        ms = ms.Tolave();
                                }
                                break;
                            case 0xC0://PES包(音频)
                                var pesAudio = new PESPacket();
                                pesAudio.SetBytes(ms);
                                AnalyzeNewFrame(pesAudio, true);
                                break;
                            case 0xBD:
                                byte[] lenbuf = new byte[2];
                                ms.Read(lenbuf, 0, 2);
                                var len = BitConverter.ToInt16(lenbuf, 0);
                                len = IPAddress.HostToNetworkOrder(len);
                                lenbuf = new byte[len];
                                ms.Read(lenbuf, 0, len);
                                break;

                        }
                    }
                    catch (Exception)
                    {
                        //if (_isWorking && SS.Base.AppConfig._D)
                        //{
                        //    //GLib.DebugEx.WriteLog(e);
                        //    throw;
                        //}

                    }
                }
            }
        }
        private MediaFrame AnalyzeNewFrame(PESPacket pes, bool isAudio)
        {
            MediaFrame result = null; ;
            if (!isAudio)
            {
                var esdata = pes.PES_Packet_Data;
                if (_listVideoPES.Count > 0 && esdata[0] == 0 && esdata[1] == 0 && esdata[2] == 0 && esdata[3] == 1)
                {
                    var stream = new MemoryStream();
                    foreach (var item in _listVideoPES)
                    {
                        stream.Write(item.PES_Packet_Data, 0, item.PES_Packet_Data.Length);
                    }
                    long tick = _listVideoPES.FirstOrDefault().GetVideoTimetick();
                    esdata = stream.ToArray();
                    var frame = CreateVideoMediaFrame(esdata, tick);
                    if (frame != null)
                    {
                        result = frame;
                        _queueFrame.Enqueue(frame);
                    }
                    _listVideoPES.Clear();
                }
                _listVideoPES.Add(pes);

            }
            else
            {
                //不处理音频
                //var esdata = pes.PES_Packet_Data;
                //if (_listAudioPES.Count > 0 && esdata[0] == 0xFF && (esdata[1] & 0xF0) == 0xF0) {
                //    var stream = new MemoryStream();
                //    foreach (var item in _listAudioPES) {
                //        stream.Write(item.PES_Packet_Data, 0, item.PES_Packet_Data.Length);
                //    }
                //    long tick = _listAudioPES.FirstOrDefault().GetAudioTimetick();
                //    esdata = stream.ToArray();
                //    var frame = CreateAudioMediaFrame(esdata, tick);
                //    _queueFrame.Enqueue(frame);
                //    _listAudioPES.Clear();
                //}
                //_listAudioPES.Add(pes);
            }
            return result;
        }

        private MediaFrame CreateVideoMediaFrame(byte[] data, long tick)
        {

            int keyFrameFlagOffset = 0;
            if (data[0] == 0 && data[1] == 0 && data[2] == 0 && data[3] == 1 && data[4] == 0x09)
            {
                keyFrameFlagOffset = 6;
            }
            if (data[keyFrameFlagOffset + 4] == 0x67 || data[keyFrameFlagOffset + 4] == 0x68 | data[keyFrameFlagOffset + 4] == 0x27)
            {//sps ,这里sps及pps及载荷会被放置到不同的pes包里
                try
                {
                    #region
                    if (this.SPS == null || this.PPS == null)
                    {
                        var tdata = data;
                        if (keyFrameFlagOffset > 0)
                        {
                            tdata = new byte[data.Length - keyFrameFlagOffset];
                            Array.Copy(data, keyFrameFlagOffset, tdata, 0, tdata.Length);
                        }
                        var sps_pps = SliceHeader.GetSPS_PPS(tdata);
                        var sps = mp4parser.h264.model.SeqParameterSet.Read(new MemoryStream(sps_pps[0], 1, sps_pps[0].Length - 1));
                        var pps = mp4parser.h264.model.PictureParameterSet.read(new MemoryStream(sps_pps[1], 1, sps_pps[1].Length - 1));
                        Width = (sps.pic_width_in_mbs_minus1 + 1) * 16 - 2 * sps.frame_crop_left_offset - 2 * sps.frame_crop_right_offset;
                        Height = (sps.pic_height_in_map_units_minus1 + 1) * 16 - 2 * sps.frame_crop_top_offset - 2 * sps.frame_crop_bottom_offset;
                        this.SPS = sps_pps[0];
                        this.PPS = sps_pps[1];
                    }
                    #endregion
                }
                catch
                {
                    if (this.SPS == null && data[keyFrameFlagOffset + 4] == 0x67)
                    {
                        SPS = data.Skip(4).ToArray();
                    }
                    if (this.PPS == null && data[keyFrameFlagOffset + 4] == 0x68)
                    {
                        PPS = data.Skip(4).ToArray();
                    }
                    if (this.SPS != null && this.PPS != null)
                    {

                        var sps = mp4parser.h264.model.SeqParameterSet.Read(new MemoryStream(this.SPS, 1, this.SPS.Length - 1));
                        // var pps = mp4parser.h264.model.PictureParameterSet.read(new MemoryStream(this.PPS, 1, this.PPS.Length - 1));
                        Width = (sps.pic_width_in_mbs_minus1 + 1) * 16 - 2 * sps.frame_crop_left_offset - 2 * sps.frame_crop_right_offset;
                        Height = (sps.pic_height_in_map_units_minus1 + 1) * 16 - 2 * sps.frame_crop_top_offset - 2 * sps.frame_crop_bottom_offset;
                    }
                    return null;
                }
                if (data[keyFrameFlagOffset + 4] == 0x67 && this.SPS != null && data.Length == this.SPS.Length + 4)
                    return null;
                if (data[keyFrameFlagOffset + 4] == 0x68 && this.PPS != null && data.Length == this.PPS.Length + 4)
                    return null;
            }
            if (this.SPS != null && this.PPS != null)
            {

                if (data[keyFrameFlagOffset + 4] == 0x65)
                {

                    var h264 = new MemoryStream();
                    h264.Write(new byte[] { 0, 0, 0, 1 }, 0, 4);
                    h264.Write(this.SPS, 0, this.SPS.Length);
                    h264.Write(new byte[] { 0, 0, 0, 1 }, 0, 4);
                    h264.Write(this.PPS, 0, this.PPS.Length);
                    // h264.Write(new byte[] { 0, 0, 0, 1 }, 0, 4);
                    h264.Write(data, 0, data.Length);

                    var mf = new MediaFrame()
                    {
                        IsAudio = 0,
                        IsKeyFrame = 1,
                        Size = (int)h264.Length,
                        Width = Width,
                        Height = Height,
                        SPSLen = SPS.Length,
                        PPSLen = PPS.Length,
                        NTimetick = tick,
                        Offset = 0,
                        Encoder = MediaFrame.H264Encoder,
                        Ex = 1,
                    };
                    mf.SetData(h264.ToArray());
                    mf.MediaFrameVersion = (byte)(mf.IsKeyFrame == 1 ? 1 : 0);
                    VideoFrameCount++;
                    return mf;
                }
                else
                {
                    if (this.SPS != null)
                    {
                        var mf = new MediaFrame()
                        {
                            IsAudio = 0,
                            IsKeyFrame = 0,
                            Size = data.Length,
                            Width = Width,
                            Height = Height,
                            SPSLen = (short)this.SPS.Length,
                            PPSLen = (short)this.PPS.Length,
                            NTimetick = tick,
                            Offset = 0,
                            Encoder = MediaFrame.H264Encoder,
                            Ex = 1,
                        };
                        mf.SetData(data);
                        mf.MediaFrameVersion = (byte)(mf.IsKeyFrame == 1 ? 1 : 0);
                        VideoFrameCount++;
                        return mf;
                    }
                }
            }
            return null;

        }

        private MediaFrame CreateAudioMediaFrame(byte[] data, long tick)
        {
            if (_firstAudioFrame)
            {
                var adts = new AAC_ADTS(data);
                var frequencys = new int[] { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 2000, 11025 };
                Frequency = frequencys[adts.MPEG_4_Sampling_Frequency_Index];
                Channel = adts.MPEG_4_Channel_Configuration;
                AudioFormat = 16;
                Samples = 0;
            }
            VideoFrameCount++;
            var mf = new MediaFrame()
            {
                IsAudio = 1,
                IsKeyFrame = 1,
                Size = data.Length,
                Channel = Channel,
                Frequency = Frequency,
                AudioFormat = AudioFormat,
                NTimetick = tick,
                Offset = 0,
                Encoder = MediaFrame.AAC_Encoder,
                Ex = (byte)(_firstAudioFrame ? 0 : 1),
                MediaFrameVersion = 1,
            };
            mf.SetData(data);
            _firstAudioFrame = false;

            return mf;

        }


    }

}
