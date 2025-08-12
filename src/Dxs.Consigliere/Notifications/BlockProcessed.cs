using MediatR;

namespace Dxs.Consigliere.Notifications;

public record BlockProcessed(int Height, string Hash): INotification;