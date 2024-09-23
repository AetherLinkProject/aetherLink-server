using System;
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
        
        private ITonIndexer _indexer;
        
        public bool IsAvailable => _isAvailiable;

        public ITonIndexer Indexer => _indexer;

        public DateTime NextCheckTime => _nextCheckTime;

        private int _checkTurn = 0; 
        
        public TonIndexerWrapper(ITonIndexer indexer)
        {
            _indexer = indexer;
        }

        public async Task<(bool, TransactionAnalysisDto<CrossChainToTonTransactionDto, TonIndexerDto>)>
            GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
        {
            try
            {
                return (true, await _indexer.GetSubsequentTransaction(tonIndexerDto));
            }catch(HttpRequestException ex)
            {
                SetDisable();
            }

            return (false, null);
        }

        public async Task<(bool, CrossChainToTonTransactionDto)> GetTransactionInfo(string txId)
        {
            try
            {
                return (true, await _indexer.GetTransactionInfo(txId));
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
                var isAvailable = await _indexer.CheckAvailable();
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
                var isRateLimiting = await _indexer.TryGetRequestAccess();
                
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