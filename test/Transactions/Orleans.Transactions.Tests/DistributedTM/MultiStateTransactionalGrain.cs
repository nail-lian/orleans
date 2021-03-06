﻿using Microsoft.Extensions.Logging;
using Orleans.Transactions.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Transactions.Tests.DistributedTM
{
    public class MaxStateTransactionalGrain : MultiStateTransactionalGrainBaseClass
    {
        public MaxStateTransactionalGrain(
            Orleans.Transactions.DistributedTM.ITransactionalStateFactory stateFactory,
            ILoggerFactory loggerFactory)
            : base(Enumerable.Range(0, TransactionTestConstants.MaxCoordinatedTransactions)
                .Select(i => stateFactory.Create<GrainData>(new TransactionalStateAttribute($"data{i}", TransactionTestConstants.TransactionStore)))
                .ToArray(),
                  loggerFactory)
        {
        }
    }

    public class DoubleStateTransactionalGrain : MultiStateTransactionalGrainBaseClass
    {
        public DoubleStateTransactionalGrain(
            [TransactionalState("data1", TransactionTestConstants.TransactionStore)]
            Orleans.Transactions.DistributedTM.ITransactionalState<GrainData> data1,
            [TransactionalState("data2", TransactionTestConstants.TransactionStore)]
            Orleans.Transactions.DistributedTM.ITransactionalState<GrainData> data2,
            ILoggerFactory loggerFactory)
            : base(new Orleans.Transactions.DistributedTM.ITransactionalState<GrainData>[2] { data1, data2 }, loggerFactory)
        {
        }
    }

    public class SingleStateTransactionalGrain : MultiStateTransactionalGrainBaseClass
    {
        public SingleStateTransactionalGrain(
            [TransactionalState("data", TransactionTestConstants.TransactionStore)]
            Orleans.Transactions.DistributedTM.ITransactionalState<GrainData> data,
            ILoggerFactory loggerFactory)
            : base(new Orleans.Transactions.DistributedTM.ITransactionalState<GrainData>[1] { data }, loggerFactory)
        {
        }
    }


    public class MultiStateTransactionalGrainBaseClass : Grain, ITransactionTestGrain
    {
        protected Orleans.Transactions.DistributedTM.ITransactionalState<GrainData>[] dataArray;
        private readonly ILoggerFactory loggerFactory;
        protected ILogger logger;

        public MultiStateTransactionalGrainBaseClass(
            Orleans.Transactions.DistributedTM.ITransactionalState<GrainData>[] dataArray,
            ILoggerFactory loggerFactory)
        {
            this.dataArray = dataArray;
            this.loggerFactory = loggerFactory;
        }

        public override Task OnActivateAsync()
        {
            this.logger = this.loggerFactory.CreateLogger(this.GetGrainIdentity().ToString());
            return base.OnActivateAsync();
        }

        public async Task Set(int newValue)
        {
            foreach(var data in this.dataArray)
            {
                await data.PerformUpdate(state =>
                {
                    this.logger.LogInformation($"Setting from {state.Value} to {newValue}.");
                    state.Value = newValue;
                    this.logger.LogInformation($"Set to {state.Value}.");
                });
            }
        }

        public async Task<int[]> Add(int numberToAdd)
        {
            var result = new int[dataArray.Length];
            for(int i = 0; i < dataArray.Length; i++)
            {
                result[i] = await dataArray[i].PerformUpdate(state =>
                {
                    this.logger.LogInformation($"Adding {numberToAdd} to {state.Value}.");
                    state.Value += numberToAdd;
                    this.logger.LogInformation($"Value after Adding {numberToAdd} is {state.Value}.");
                    return state.Value;
                });
            }
            return result;
        }

        public async Task<int[]> Get()
        {
            var result = new int[dataArray.Length];
            for (int i = 0; i < dataArray.Length; i++)
            {
                result[i] = await dataArray[i].PerformRead(state =>
                {
                    this.logger.LogInformation($"Get {state.Value}.");
                    return state.Value;
                });
            }
            return result;              
        }

        public async Task AddAndThrow(int numberToAdd)
        {
            await Add(numberToAdd);
            throw new Exception($"{GetType().Name} test exception");
        }

        public Task Deactivate()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }
    }
}
