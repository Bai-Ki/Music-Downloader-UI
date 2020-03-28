﻿using MusicDownloader_New.Json;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace MusicDownloader_New.Library
{
    public class Music
    {
        List<int> version = new List<int> { 1, 0, 0 };
        const string ApiUrl = "http://116.85.33.135:3000/";
        public Setting setting;
        public List<DownloadList> downloadlist = new List<DownloadList>();
        string cookie = "";
        Thread th_Download;
        public delegate void UpdateDownloadPageEventHandler();
        public event UpdateDownloadPageEventHandler UpdateDownloadPage;

        /// <summary>
        /// 获取更新数据
        /// </summary>
        /// <returns></returns>
        void Update()
        {
            WebClient wc = new WebClient();
            StreamReader sr = new StreamReader(wc.OpenRead("http://nitian1207.cn/update/MusicDownload.json"));
            Update update = JsonConvert.DeserializeObject<Update>(sr.ReadToEnd());
            cookie = update.Cookie;
            bool needupdate = false;
            if (update.Version[0] > version[0])
            {
                needupdate = true;
            }
            else if (update.Version[1] > version[1])
            {
                needupdate = true;
            }
            else if (update.Version[2] > version[2])
            {
                needupdate = true;
            }
            if (needupdate)
            { 
                
            }
        }

        /// <summary>
        /// 构造函数 需要提供设置参数
        /// </summary>
        /// <param name="setting"></param>
        public Music(Setting setting)
        {
            this.setting = setting;
            Update();
        }

        /// <summary>
        /// 线程调用的搜索
        /// </summary>
        public List<MusicInfo> Search(string Key)
        {
            List<MusicInfo> searchItem = new List<MusicInfo>();
            string key = Key;
            int quantity = Int32.Parse(setting.SearchQuantity);
            int pagequantity = quantity / 100;
            int remainder = quantity % 100;

            if (remainder == 0)
            {
                remainder = 100;
            }
            if (pagequantity == 0)
            {
                pagequantity = 1;
            }

            for (int i = 0; i < pagequantity; i++)
            {
                if (i == pagequantity - 1 && pagequantity >= 1)
                {
                    searchItem.AddRange(Search(key, i + 1, remainder));
                }
                else
                {
                    searchItem.AddRange(Search(key, i + 1, 100));
                }
            }
            return searchItem;
        }

        /// <summary>
        /// 带cookie访问
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        string GetHTML(string url)
        {
            WebClient wc = new WebClient();
            wc.Headers.Add(HttpRequestHeader.Cookie, cookie);
            Stream s = wc.OpenRead(url);
            StreamReader sr = new StreamReader(s);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// 搜索歌曲
        /// </summary>
        /// <param name="Key"></param>
        /// <param name="Page"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        List<MusicInfo> Search(string Key, int Page = 1, int limit = 100)
        {
            if (Key == null || Key == "")
            {
                return null;
            }
            WebClient wc = new WebClient();
            string offset = ((Page - 1) * 100).ToString();
            string url = ApiUrl + "search?keywords=" + Key + "&limit=" + limit.ToString() + "&offset=" + offset;
            string json = GetHTML(url);
            if (json == null || json == "")
            {
                return null;
            }
            Json.SearchResultJson.Root srj = JsonConvert.DeserializeObject<Json.SearchResultJson.Root>(json);
            List<Json.MusicInfo> ret = new List<Json.MusicInfo>();
            string ids = "";
            for (int i = 0; i < srj.result.songs.Count; i++)
            {
                ids += srj.result.songs[i].id + ",";
            }
            string _u = ApiUrl + "song/detail?ids=" + ids.Substring(0, ids.Length - 1);
            string j = GetHTML(_u);
            Json.MusicDetails.Root mdr = JsonConvert.DeserializeObject<Json.MusicDetails.Root>(j);
            //string u = ApiUrl + "song/url?id=" + ids.Substring(0, ids.Length - 1) + "&br=" + setting.DownloadQuality;
            //Json.GetUrl.Root urls = JsonConvert.DeserializeObject<Json.GetUrl.Root>(GetHTML(u));

            for (int i = 0; i < mdr.songs.Count; i++)
            {
                string singer = "";
                List<string> singerid = new List<string>();
                //string _url = "";

                for (int x = 0; x < mdr.songs[i].ar.Count; x++)
                {
                    singer += mdr.songs[i].ar[x].name + "、";
                    singerid.Add(mdr.songs[i].ar[x].id.ToString());
                }

                //for (int x = 0; x < urls.data.Count; x++)
                //{
                //    if (urls.data[x].id == mdr.songs[i].id)
                //    {
                //        _url = urls.data[x].url;
                //    }
                //}

                Json.MusicInfo mi = new Json.MusicInfo()
                {
                    Album = mdr.songs[i].al.name,
                    Id = mdr.songs[i].id,
                    LrcUrl = ApiUrl + "lyric?id=" + mdr.songs[i].id,
                    PicUrl = mdr.songs[i].al.picUrl,
                    Singer = singer.Substring(0, singer.Length - 1),
                    Title = mdr.songs[i].name,
                    //Url = _url
                };
                ret.Add(mi);
            }
            return ret;
        }

        public void Download(List<DownloadList> dl)
        {
            string ids = "";
            foreach (DownloadList d in dl)
            {
                ids += d.Id.ToString() + ",";
                d.State = "准备下载";
                UpdateDownloadPage();
            }
            ids = ids.Substring(0, ids.Length - 1);
            string u = ApiUrl + "song/url?id=" + ids + "&br=" + dl[0].Quality;
            Json.GetUrl.Root urls = JsonConvert.DeserializeObject<Json.GetUrl.Root>(GetHTML(u));
            for (int i = 0; i < urls.data.Count; i++)
            {
                dl[i].Url = urls.data[i].url;
            }
            downloadlist.AddRange(dl);
            UpdateDownloadPage();
            if (th_Download != null && th_Download?.ThreadState != ThreadState.Running)
                th_Download.Start();
            else
            {
                th_Download = new Thread(_Download);
                th_Download.Start();
            }
        }

        string NameCheck(string name)
        {
            string re = name.Replace("*", " ");
            re = re.Replace("\\", " ");
            re = re.Replace("\"", " ");
            re = re.Replace("<", " ");
            re = re.Replace(">", " ");
            re = re.Replace("|", " ");
            re = re.Replace("?", " ");
            re = re.Replace("/", ",");
            re = re.Replace(":", "：");
            //re = re.Replace("-", "_");
            return re;
        }

        private void _Download()
        {
            while (downloadlist.Count != 0)
            {
                downloadlist[0].State = "正在下载音乐";
                UpdateDownloadPage();
                string savepath = "";
                string filename = ""; ;
                switch (setting.SaveNameStyle)
                {
                    case 0:
                        if (downloadlist[0].Url.IndexOf("flac") != -1)
                            filename = NameCheck(downloadlist[0].Title) + " - " + NameCheck(downloadlist[0].Singer) + ".flac";
                        else
                            filename = NameCheck(downloadlist[0].Title) + " - " + NameCheck(downloadlist[0].Singer) + ".mp3";
                        break;
                    case 1:
                        if (downloadlist[0].Url.IndexOf("flac") != -1)
                            filename = NameCheck(downloadlist[0].Singer) + " - " + NameCheck(downloadlist[0].Title) + ".flac";
                        else
                            filename = NameCheck(downloadlist[0].Singer) + " - " + NameCheck(downloadlist[0].Title) + ".mp3";
                        break;
                }
                switch (setting.SavePathStyle)
                {
                    case 0:
                        savepath = setting.SavePath;
                        break;
                    case 1:
                        savepath = setting.SavePath + "\\" + NameCheck(downloadlist[0].Singer);
                        break;
                    case 2:
                        savepath = setting.SavePath + "\\" + NameCheck(downloadlist[0].Singer) + "\\" + NameCheck(downloadlist[0].Album);
                        break;
                }
                if (Directory.Exists(savepath))
                    Directory.CreateDirectory(savepath);

                if (downloadlist[0].IfDownloadMusic)
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(downloadlist[0].Url, savepath + "\\" + filename);
                    }
                }
                if (downloadlist[0].IfDownloadLrc)
                {
                    downloadlist[0].State = "正在下载歌词";
                    UpdateDownloadPage();
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(downloadlist[0].LrcUrl, savepath + "\\" + filename.Replace(".flac", ".lrc").Replace(".mp3", ".lrc"));
                    }
                }
                if (downloadlist[0].IfDownloadPic)
                {
                    downloadlist[0].State = "正在下载图片";
                    UpdateDownloadPage();
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(downloadlist[0].PicUrl, savepath + "\\" + filename.Replace(".flac", ".jpg").Replace(".mp3", ".jpg"));
                    }
                }
                if (filename.IndexOf(".mp3") != -1)
                {
                    var tfile = TagLib.File.Create(savepath + "\\" + filename);
                    tfile.Tag.Title = downloadlist[0].Title;
                    tfile.Tag.Performers = new string[] { downloadlist[0].Singer };
                    tfile.Tag.Album = downloadlist[0].Album;
                    if (downloadlist[0].IfDownloadPic && System.IO.File.Exists(savepath + "\\" + filename.Replace(".flac", "").Replace(".mp3", "") + ".jpg"))
                    {
                        TagLib.Picture pic = new TagLib.Picture();
                        pic.Type = TagLib.PictureType.FrontCover;
                        pic.Description = "Cover";
                        pic.MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg;
                        pic.Data = TagLib.ByteVector.FromPath(savepath + "\\" + filename.Replace(".flac", "").Replace(".mp3", "") + ".jpg");
                        tfile.Tag.Pictures = new TagLib.IPicture[] { pic };
                    }
                    tfile.Save();
                }
                else
                {
                    var tfile = TagLib.Flac.File.Create(savepath + "\\" + filename);
                    tfile.Tag.Title = downloadlist[0].Title;
                    tfile.Tag.Performers = new string[] { downloadlist[0].Singer };
                    tfile.Tag.Album = downloadlist[0].Album;

                    if (downloadlist[0].IfDownloadPic && System.IO.File.Exists(savepath + "\\" + filename.Replace(".flac", "").Replace(".mp3", "") + ".jpg"))
                    {
                        TagLib.Picture pic = new TagLib.Picture();
                        pic.Type = TagLib.PictureType.FrontCover;
                        pic.Description = "Cover";
                        pic.MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg;
                        pic.Data = TagLib.ByteVector.FromPath(savepath + "\\" + filename.Replace(".flac", "").Replace(".mp3", "") + ".jpg");
                        tfile.Tag.Pictures = new TagLib.IPicture[] { pic };
                    }
                    tfile.Save();
                }
                downloadlist.Remove(downloadlist[0]);
                UpdateDownloadPage();
            }
        }
    }
}
