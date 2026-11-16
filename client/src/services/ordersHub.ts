import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr'

export interface OrderStatusNotification {
  orderId: string
  status: number | string
  message?: string
}

export function createOrdersHub(
  token: string,
  onStatusChanged: (notification: OrderStatusNotification) => void,
): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/orders', {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('OrderStatusChanged', onStatusChanged)

  return connection
}
