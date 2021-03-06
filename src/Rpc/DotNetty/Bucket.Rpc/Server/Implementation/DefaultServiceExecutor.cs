﻿using Bucket.Rpc.Messages;
using Bucket.Rpc.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Bucket.Rpc.Server.Implementation
{
    public class DefaultServiceExecutor : IServiceExecutor
    {
        #region Field

        private readonly IServiceEntryLocate _serviceEntryLocate;
        private readonly ILogger<DefaultServiceExecutor> _logger;

        #endregion Field

        #region Constructor

        public DefaultServiceExecutor(IServiceEntryLocate serviceEntryLocate, ILogger<DefaultServiceExecutor> logger)
        {
            _serviceEntryLocate = serviceEntryLocate;
            _logger = logger;
        }

        #endregion Constructor

        #region Implementation of IServiceExecutor

        /// <summary>
        /// 执行。
        /// </summary>
        /// <param name="sender">消息发送者。</param>
        /// <param name="message">调用消息。</param>
        public async Task ExecuteAsync(IMessageSender sender, TransportMessage message)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("接收到消息。");

            if (!message.IsInvokeMessage())
                return;

            RemoteInvokeMessage remoteInvokeMessage;
            try
            {
                remoteInvokeMessage = message.GetContent<RemoteInvokeMessage>();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "将接收到的消息反序列化成 TransportMessage<RemoteInvokeMessage> 时发送了错误。");
                await SendRemoteInvokeErrorResult(sender, message.Id, 500, $"将接收到的消息反序列化成 TransportMessage<RemoteInvokeMessage> 时发送了错误。");
                return;
            }

            var entry = _serviceEntryLocate.Locate(remoteInvokeMessage);

            if (entry == null)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError($"根据服务Id：{remoteInvokeMessage.ServiceId}，找不到服务条目。");
                await SendRemoteInvokeErrorResult(sender, message.Id, 404, $"根据服务Id：{remoteInvokeMessage.ServiceId}，找不到服务条目");
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("准备执行本地逻辑。");

            var resultMessage = new RemoteInvokeResultMessage();

            // 是否需要等待执行。
            if (entry.Descriptor.WaitExecution())
            {
                // 执行本地代码。
                await LocalExecuteAsync(entry, remoteInvokeMessage, resultMessage);
                // 向客户端发送调用结果。
                await SendRemoteInvokeResult(sender, message.Id, resultMessage);
            }
            else
            {
                // 通知客户端已接收到消息。
                await SendRemoteInvokeResult(sender, message.Id, resultMessage);
                // 确保新起一个线程执行，不堵塞当前线程。
                await Task.Factory.StartNew(async () =>
                {
                    // 执行本地代码。
                    await LocalExecuteAsync(entry, remoteInvokeMessage, resultMessage);
                }, TaskCreationOptions.LongRunning);
            }
        }

        #endregion Implementation of IServiceExecutor

        #region Private Method

        private async Task LocalExecuteAsync(ServiceEntry entry, RemoteInvokeMessage remoteInvokeMessage, RemoteInvokeResultMessage resultMessage)
        {
            try
            {
                var result = await entry.Func(remoteInvokeMessage.Parameters);
                var task = result as Task;

                if (task == null)
                {
                    resultMessage.Result = result;
                }
                else
                {
                    task.Wait();

                    var taskType = task.GetType().GetTypeInfo();
                    if (taskType.IsGenericType)
                        resultMessage.Result = taskType.GetProperty("Result").GetValue(task);
                }
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError(exception, "执行本地逻辑时候发生了错误。");
                resultMessage.StatusCode = 500;
                resultMessage.ExceptionMessage = GetExceptionMessage(exception);
            }
        }

        private async Task SendRemoteInvokeResult(IMessageSender sender, string messageId, RemoteInvokeResultMessage resultMessage)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("准备发送响应消息。");

                await sender.SendAndFlushAsync(TransportMessage.CreateInvokeResultMessage(messageId, resultMessage));
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("响应消息发送成功。");
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError(exception, "发送响应消息时候发生了异常。");
            }
        }

        private async Task SendRemoteInvokeErrorResult(IMessageSender sender, string messageId, int statusCode, string errorMessage)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("准备发送响应消息。");

                await sender.SendAndFlushAsync(TransportMessage.CreateInvokeResultMessage(messageId, new RemoteInvokeResultMessage { StatusCode = statusCode, ExceptionMessage = errorMessage }));
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("响应消息发送成功。");
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError(exception, "发送响应消息时候发生了异常。");
            }
        }
        private static string GetExceptionMessage(Exception exception)
        {
            if (exception == null)
                return string.Empty;

            var message = exception.Message;
            if (exception.InnerException != null)
            {
                message += "|InnerException:" + GetExceptionMessage(exception.InnerException);
            }
            return message;
        }

        #endregion Private Method
    }
}
