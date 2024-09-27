using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Dtos;
using Microsoft.AspNetCore.Server.HttpSys;
using TonSdk.Core;
using TonSdk.Core.Boc;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public class TonIndexerWrapper
{
        private readonly Object _lock = new object();

        private bool _isAvailable = true;
        
        private DateTime _nextCheckTime;
        
        private readonly ITonIndexerProvider _indexerBase;
        
        public ITonIndexerProvider IndexerBase => _indexerBase;

        private int _checkTurn; 
        
        public TonIndexerWrapper(ITonIndexerProvider indexerBase)
        {
            _indexerBase = indexerBase;
        }

        public async Task<(bool, (List<CrossChainToTonTransactionDto>, TonIndexerDto))>
            GetSubsequentTransaction(TonIndexerDto tonIndexerDto)
        {
            try
            {
                return (true, await _indexerBase.GetSubsequentTransaction(tonIndexerDto));
            }
            catch (HttpRequestException)
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

        public async Task<(bool, uint?)> GetAddressSeqno(Address address)
        {
            try
            {
                return (true, await _indexerBase.GetAddressSeqno(address));
            }
            catch (HttpRequestException)
            {
                SetDisable();
            }

            return (false, null);
        }

        public async Task<(bool, string)> CommitTransaction(Cell bodyCell)
        {
            try
            {
                return (true, await _indexerBase.CommitTransaction(bodyCell));
            }
            catch (HttpRequestException)
            {
                SetDisable();
            }

            return (false, null);
        }

        public async Task<bool> CheckAvailable()
        {
            return _isAvailable && await TryGetRequestAccess();
        }

        public async Task<bool> NeedCheckConnection()
        {
            if (!_isAvailable && DateTime.UtcNow >= _nextCheckTime)
            {
                return await _indexerBase.TryGetRequestAccess();
            }

            return false;
        }

        public async Task CheckConnection()
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
        }

        private async Task<bool> TryGetRequestAccess()
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