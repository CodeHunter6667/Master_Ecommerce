# MasterEcommerce - Aplicativo de Ecommerce com Microsservi�os

## Arquitetura

O aplicativo est� estruturado com os seguintes microsservi�os:

1. **API Principal**: Recebe requisi��es para cria��o de pedidos
2. **PaymentService**: Processa pagamentos (mock com resultado aleat�rio)
3. **InventoryService**: Verifica disponibilidade de estoque
4. **EmailService**: Envia emails para clientes
5. **ShippingService**: Agenda e processa entregas
6. **OrderOrchestrator**: Coordena o fluxo entre os servi�os

## Comunica��o

- **RabbitMQ**: Usado para comunica��o ass�ncrona entre servi�os
- **Filas utilizadas**:
  - `order.created`: Pedido criado
  - `payment.processed`: Resultado do pagamento
  - `inventory.checked`: Resultado da verifica��o de estoque
  - `email.send`: Envio de emails
  - `shipping.schedule`: Agendamento de envio
  - `shipping.processed`: Confirma��o de envio

## Fluxo do Pedido

1. Cliente faz POST para `/api/orders`
2. API publica mensagem `order.created`
3. PaymentService e InventoryService processam simultaneamente
4. Orquestrador recebe respostas e decide:
   - **Sucesso**: Email de confirma��o + agendamento de envio
   - **Falha**: Email informando o motivo da falha
5. ShippingService processa e envia email de confirma��o de envio

## Como Testar

### Pr�-requisitos
- RabbitMQ rodando localmente (localhost:5672)
  - Instalar: https://www.rabbitmq.com/download.html
  - Ou usar Docker: `docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`

### Executar a aplica��o
```bash
dotnet run
```

### Exemplo de requisi��o para criar pedido

POST `/api/orders`
```json
{
  "customerEmail": "cliente@email.com",
  "customerName": "Jo�o Silva",
  "items": [
    {
      "productId": 1,
      "productName": "Notebook Dell",
      "price": 2500.00,
      "quantity": 1
    },
    {
      "productId": 2,
      "productName": "Mouse Wireless",
      "price": 50.00,
      "quantity": 2
    }
  ]
}
```

### Produtos de exemplo no estoque
- Produto 1: 10 unidades
- Produto 2: 5 unidades
- Produto 3: 0 unidades (sem estoque)
- Produto 4: 20 unidades
- Produto 5: 3 unidades

## Observa��o
- O PaymentService tem 70% de chance de aprovar o pagamento
- Os logs mostram todo o fluxo de processamento
- Emails s�o simulados (aparecem nos logs)