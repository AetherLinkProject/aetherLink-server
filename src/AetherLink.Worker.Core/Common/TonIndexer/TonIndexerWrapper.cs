using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Microsoft.AspNetCore.Server.HttpSys;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerWrapper
{
        private Object _lock = new object();

        private bool _isAvailiable = true;
        
        private DateTime _nextCheckTime;
        
        private TonIndexerBase _indexerBase;
        
        public bool IsAvailable => _isAvailiable;

        public TonIndexerBase IndexerBase => _indexerBase;

        public DateTime NextCheckTime => _nextCheckTime;

        private int _checkTurn = 0; 
        
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
            }catch(HttpRequestException ex)
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
            }catch(HttpRequestException ex)
            {
                SetDisable();
            }

            return (false, null);
        }

        public bool CanCheckAvailable()
        {
            return !_isAvailiable && DateTime.UtcNow >= _nextCheckTime;
        }
        
        public async Task<bool> CheckAvailable()
        {
            try
            {
                var isAvailable = await _indexerBase.CheckAvailable();
                if (isAvailable && !_isAvailiable)
                {
                    SetAvailable();
                }

                if (!isAvailable)
                {
                    SetDisable();
                }
            }
            catch (HttpRequestException ex)
            {
                SetDisable();
            }

            return _isAvailiable;
        }

        public async Task<bool> TryGetRequestAccess()
        {
            try
            {
                var isRateLimiting = await _indexerBase.TryGetRequestAccess();
                
                return isRateLimiting;
            }
            catch (HttpSysException ex)
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

                if (_isAvailiable)
                {
                    _checkTurn = 0;
                }

                _checkTurn += 1;
                _nextCheckTime = DateTime.UtcNow.AddSeconds(Math.Pow(2, _checkTurn) * 10);
                _isAvailiable = false;
            }
        }

        public void SetAvailable()
        {
            lock (_lock)
            {
                if (!_isAvailiable)
                {
                    _isAvailiable = true;
                    _checkTurn = 0;
                }
            }
        }
}