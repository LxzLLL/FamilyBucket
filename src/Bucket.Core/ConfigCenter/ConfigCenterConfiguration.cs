﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Bucket.ConfigCenter
{
    public class ConfigCenterConfiguration
    {
        public string AppId { set; get; }
        public string AppSercet { set; get; }
        public string ServerUrl { set; get; }
        public int RefreshInteval { set; get; }
        public bool RedisListener { set; get; }
        public string RedisConnectionString { set; get; }
        public bool UseServiceDiscovery { set; get; }
        public string ServiceName { set; get; }
    }
}
