# Comandos para Testar a API MasterEcommerce

## Pr�-requisitos
1. **RabbitMQ instalado e rodando**:
   - Instalar RabbitMQ: https://www.rabbitmq.com/download.html
   - Ou usar Docker: `docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`
   - Verificar se est� rodando: acesse http://localhost:15672 (usu�rio: guest, senha: guest)

## Executar a Aplica��o
```bash
dotnet run
```

## Testar com cURL

### 1. Criar um pedido com produtos dispon�veis
```bash
curl -X POST "https://localhost:7000/api/orders" \
-H "Content-Type: application/json" \
-d '{
  "customerEmail": "cliente@exemplo.com",
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
}'
```

### 2. Criar um pedido com produto sem estoque
```bash
curl -X POST "https://localhost:7000/api/orders" \
-H "Content-Type: application/json" \
-d '{
  "customerEmail": "cliente2@exemplo.com",
  "customerName": "Maria Santos",
  "items": [
    {
      "productId": 3,
      "productName": "Tablet Android",
      "price": 800.00,
      "quantity": 1
    }
  ]
}'
```

### 3. Consultar status de um pedido
```bash
curl -X GET "https://localhost:7000/api/orders/{order-id}"
```

## Testar com Postman/Insomnia

### POST /api/orders
- **URL**: `https://localhost:7000/api/orders`
- **Method**: POST
- **Headers**: `Content-Type: application/json`
- **Body** (raw JSON):
```json
{
  "customerEmail": "teste@exemplo.com",
  "customerName": "Cliente Teste",
  "items": [
    {
      "productId": 1,
      "productName": "Produto Teste",
      "price": 100.00,
      "quantity": 1
    }
  ]
}
```

## Swagger UI
Ap�s executar a aplica��o, acesse: https://localhost:7000/swagger

## Monitorar Logs
Os logs mostrar�o todo o fluxo:
1. Pedido recebido
2. Processamento de pagamento
3. Verifica��o de estoque
4. Decis�o do orquestrador
5. Envio de emails
6. Processamento de entrega

## Cen�rios de Teste

### Cen�rio 1: Pedido Aprovado
- Use produtos 1, 2, 4 ou 5
- Pagamento tem 70% de chance de aprova��o
- Se aprovado, receber� email de confirma��o e agendar� entrega

### Cen�rio 2: Falha no Estoque
- Use produto 3 (sem estoque)
- Receber� email informando indisponibilidade

### Cen�rio 3: Falha no Pagamento
- Use qualquer produto dispon�vel
- 30% de chance de reprova��o
- Receber� email informando falha no pagamento

### Cen�rio 4: Quantidade Maior que Estoque
- Use produto 5 com quantidade > 3
- Receber� email informando estoque insuficiente