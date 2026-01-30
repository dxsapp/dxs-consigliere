using MediatR;

namespace Dxs.Consigliere.Notifications;

public record TransactionDeleted(string Id) : INotification;
