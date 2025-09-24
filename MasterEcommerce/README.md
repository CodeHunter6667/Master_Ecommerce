# MasterEcommerce - Aplicativo de Ecommerce com Microsserviços

## Arquitetura

O aplicativo está estruturado com os seguintes microsserviços:

1. **API Principal**: Recebe requisições para criação de pedidos
2. **PaymentService**: Processa pagamentos (mock com resultado aleatório)
3. **InventoryService**: Verifica disponibilidade de estoque
4. **EmailService**: Envia emails para clientes
5. **ShippingService**: Agenda e processa entregas
6. **OrderOrchestrator**: Coordena o fluxo entre os serviços

## Comunicação

- **RabbitMQ**: Usado para comunicação assíncrona entre serviços
- **Filas utilizadas**:
  - `order.created`: Pedido criado
  - `payment.processed`: Resultado do pagamento
  - `inventory.checked`: Resultado da verificação de estoque
  - `email.send`: Envio de emails
  - `shipping.schedule`: Agendamento de envio
  - `shipping.processed`: Confirmação de envio

## Fluxo do Pedido

1. Cliente faz POST para `/api/orders`
2. API publica mensagem `order.created`
3. PaymentService e InventoryService processam simultaneamente
4. Orquestrador recebe respostas e decide:
   - **Sucesso**: Email de confirmação + agendamento de envio
   - **Falha**: Email informando o motivo da falha
5. ShippingService processa e envia email de confirmação de envio

## Como Testar

### Pré-requisitos
- RabbitMQ rodando localmente (localhost:5672)
  - Instalar: https://www.rabbitmq.com/download.html
  - Ou usar Docker: `docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`

### Executar a aplicação
```bash
dotnet run
```

### Exemplo de requisição para criar pedido

POST `/api/orders`
```json
{
  "customerEmail": "cliente@email.com",
  "customerName": "João Silva",
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

## Observação
- O PaymentService tem 70% de chance de aprovar o pagamento
- Os logs mostram todo o fluxo de processamento
- Emails são simulados (aparecem nos logs)