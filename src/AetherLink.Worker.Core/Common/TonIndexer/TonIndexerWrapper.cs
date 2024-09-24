using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Microsoft.AspNetCore.Server.HttpSys;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerWrapper
{
        private readonly Object _lock = new object();

        private bool _isAvailable = true;
        
        private DateTime _nextCheckTime;
        
        private readonly TonIndexerBase _indexerBase;
        
        public bool IsAvailable => _isAvailable;

        public TonIndexerBase IndexerBase => _indexerBase;

        public DateTime NextCheckTime => _nextCheckTime;

        private int _checkTurn; 
        
        public TonIndexerWrapper(TonIndexerBase indexerBase)
        {
            _indexerBase = indexerBase;
        }

        public async Task<(bool, (List<CrossChainToTonTransactionDto>, TonIndexerDto))>
            GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
        {
            try
            {
                return (true, await _indexerBase.GetSubsequentTransaction(tonIndexerDto));
            }catch(HttpRequestException)
            {
                SetDisable();
            }

            return (false, (null,null));
        }

        public async Task<(bool, CrossChainToTonTransactionDto)> GetTransactionInfo(string txId)
        {
            try
            {
                return (true, await _indexerBase.GetTransactionInfo(txId));
            }catch(HttpRequestException)
            {
                SetDisable();
            }

            return (false, null);
        }

        public bool CanCheckAvailable()
        {
            return !_isAvailable && DateTime.UtcNow >= _nextCheckTime;
        }
        
        public async Task<bool> CheckAvailable()
        {
            try
            {
                var isAvailable = await _indexerBase.CheckAvailable();
                if (isAvailable && !_isAvailable)
                {
                    SetAvailable();
                }

                if (!isAvailable)
                {
                    SetDisable();
                }
            }
            catch (HttpRequestException)
            {
                SetDisable();
            }

            return _isAvailable;
        }

        public async Task<bool> TryGetRequestAccess()
        {
            try
            {
                var isRateLimiting = await _indexerBase.TryGetRequestAccess();
                
                return isRateLimiting;
            }
            catch (HttpSysException)
            {
                SetDisable();
            }

            return true;
        }
        
        private void SetDisable()
        {
            lock (_lock)
            {
                if (DateTime.UtcNow < _nextCheckTime)
                {
                    return;
                }

                if (_isAvailable)
                {
                    _checkTurn = 0;
                }

                _checkTurn += 1;
                _nextCheckTime = DateTime.UtcNow.AddSeconds(Math.Pow(2, _checkTurn) * 10);
                _isAvailable = false;
            }
        }

        private void SetAvailable()
        {
            lock (_lock)
            {
                if (!_isAvailable)
                {
                    _isAvailable = true;
                    _checkTurn = 0;
                }
            }
        }
}