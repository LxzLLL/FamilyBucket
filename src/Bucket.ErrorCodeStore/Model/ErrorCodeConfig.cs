﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Bucket.ErrorCodeStore.Model
{
    public class ErrorCodeConfig
    {
        public ConcurrentDictionary<string, string> KV { get; set; }
    }
}
