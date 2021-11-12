﻿using System.Threading.Tasks;
using Smartstore.Core.Rules;

namespace Smartstore.Core.Checkout.Rules.Impl
{
    internal class RewardPointsBalanceRule : IRule
    {
        public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
        {
            var rewardPointsBalance = context.Customer.GetRewardPointsBalance();

            return Task.FromResult(expression.Operator.Match(rewardPointsBalance, expression.Value));
        }
    }
}