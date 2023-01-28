using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiSoft.Tools.Extensions;
using AiSoft.Tools.Security.Internal;
using SAEA.Common.Caching;

namespace AiSoft.Socket.Server.Collection
{
    internal class ClientList
    {
        private MemoryCache<ClientInfo> _cache;

        private object _syncLocker;

        /// <summary>
        /// 初始化
        /// </summary>
        public ClientList()
        {
            _syncLocker = new object();
            _cache = new MemoryCache<ClientInfo>();
        }

        public ClientInfo Get(string id)
        {
            lock (_syncLocker)
            {
                return _cache.Get(id);
            }
        }

        public bool Set(string id, AESKey key)
        {
            lock (_syncLocker)
            {
                var ci = _cache.Get(id);
                if (ci != null)
                {
                    ci.EncryptKey = key;
                }
                else
                {
                    ci = new ClientInfo {EncryptKey = key};
                    _cache.Set(id, ci, TimeSpan.FromDays(1));
                }
                return true;
            }
        }

        public bool Del(string id)
        {
            lock (_syncLocker)
            {
                return _cache.Del(id);
            }
        }

        public void Clear()
        {
            lock (_syncLocker)
            {
                _cache.Clear();
            }
        }

        public List<string> ToList()
        {
            lock (_syncLocker)
            {
                var tokenList = _cache.ToList();
                var idList = new List<string>();
                Parallel.ForEach(tokenList, t => { idList.Add(t.Key); });
                return idList;
            }
        }

        public byte[] Encrypt(string id, byte[] data, bool isEncrypt)
        {
            lock (_syncLocker)
            {
                var ci = _cache.Get(id);
                if (ci != null)
                {
                    _cache.Active(id, TimeSpan.FromDays(1));
                    return isEncrypt ? data.EncryptTo(ci.EncryptKey.Key, ci.EncryptKey.IV) : data;
                }
                return isEncrypt ? data.EncryptTo() : data;
            }
        }

        public byte[] Decrypt(string id, byte[] data, bool isEncrypt)
        {
            lock (_syncLocker)
            {
                var ci = _cache.Get(id);
                if (ci != null)
                {
                    _cache.Active(id, TimeSpan.FromDays(1));
                    return isEncrypt ? data.DecryptTo(ci.EncryptKey.Key, ci.EncryptKey.IV) : data;
                }
                return isEncrypt ? data.DecryptTo() : data;
            }
        }
    }

    internal class ClientInfo
    {
        /// <summary>
        /// 加密密匙
        /// </summary>
        public AESKey EncryptKey { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public ClientInfo()
        {
            EncryptKey = new AESKey();
        }
    }
}