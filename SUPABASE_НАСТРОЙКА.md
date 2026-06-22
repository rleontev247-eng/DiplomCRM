# 🌐 НАСТРОЙКА SUPABASE ДЛЯ MyFirstCRM

## 🎯 ЧТО НУЖНО СДЕЛАТЬ

### Шаг 1: Создание таблиц в Supabase

1. Зайдите в ваш проект на **https://supabase.com**
2. Откройте **SQL Editor**
3. Выполните этот SQL код:

```sql
-- Создание таблицы компаний
CREATE TABLE companies (
  id SERIAL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  description TEXT,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);

-- Создание таблицы клиентов
CREATE TABLE clients (
  id SERIAL PRIMARY KEY,
  company_id INTEGER REFERENCES companies(id),
  name VARCHAR(255) NOT NULL,
  phone VARCHAR(50),
  email VARCHAR(255),
  notes TEXT,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);

-- Создание таблицы сделок
CREATE TABLE deals (
  id SERIAL PRIMARY KEY,
  company_id INTEGER REFERENCES companies(id),
  client_id INTEGER REFERENCES clients(id),
  title VARCHAR(255) NOT NULL,
  description TEXT,
  amount DECIMAL(10,2),
  status VARCHAR(50) DEFAULT 'New',
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);

-- Включение Row Level Security
ALTER TABLE companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE clients ENABLE ROW LEVEL SECURITY;
ALTER TABLE deals ENABLE ROW LEVEL SECURITY;

-- Создание политики для чтения/записи
CREATE POLICY "Enable insert for all users" ON companies FOR INSERT WITH CHECK (true);
CREATE POLICY "Enable select for all users" ON companies FOR SELECT USING (true);
CREATE POLICY "Enable update for all users" ON companies FOR UPDATE USING (true);

CREATE POLICY "Enable insert for all users" ON clients FOR INSERT WITH CHECK (true);
CREATE POLICY "Enable select for all users" ON clients FOR SELECT USING (true);
CREATE POLICY "Enable update for all users" ON clients FOR UPDATE USING (true);

CREATE POLICY "Enable insert for all users" ON deals FOR INSERT WITH CHECK (true);
CREATE POLICY "Enable select for all users" ON deals FOR SELECT USING (true);
CREATE POLICY "Enable update for all users" ON deals FOR UPDATE USING (true);
```

### Шаг 2: Получение данных для подключения

1. В проекте Supabase перейдите в **Settings → API**
2. Скопируйте:
   - **Project URL**: `https://wekunpetbcusdyxtlydn.supabase.co`
   - **anon public key**: `eyJhbGciOiJIUzI1...`

### Шаг 3: Настройка в MyFirstCRM

1. Запустите MyFirstCRM
2. Откройте **🌐 Настроить развертывание**
3. Выберите **☁️ Облачный режим**
4. Введите:
   ```
   URL сервера: https://wekunpetbcusdyxtlydn.supabase.co
   API ключ: sb_secret_hEXF...
   ```
5. Нажмите **🔍 Проверить подключение**
6. Должно появиться: **✅ Подключение успешно!**

---

## 📋 ПОШАГОВАЯ ИНСТРУКЦИЯ

### Компьютер 1:
1. Настройте Supabase (SQL код выше)
2. Введите URL и API ключ в MyFirstCRM
3. Создайте тестовую компанию
4. Проверьте, что она сохранилась

### Компьютер 2:
1. Запустите MyFirstCRM
2. Введите **ТЕ ЖЕ** URL и API ключ
3. Нажмите "Проверить подключение"
4. Вы должны увидеть ту же компанию

---

## ⚠️ ВАЖНО

1. **API ключ**: Используйте `anon public key`, не `service_role key`
2. **URL**: Полный URL с https://
3. **Таблицы**: SQL код нужно выполнить ОДИН РАЗ
4. **Права доступа**: Row Level Security должен быть включен

---

## 🔧 ЕСЛИ НЕ РАБОТАЕТ

### Ошибка "Подключение не удалось"
1. Проверьте URL: должен быть `https://wekunpetbcusdyxtlydn.supabase.co`
2. Проверьте API ключ: скопируйте полностью без пробелов
3. Проверьте интернет-соединение

### Ошибка "Таблица не найдена"
1. Выполните SQL код в Supabase SQL Editor
2. Проверьте, что таблицы создались: `SELECT * FROM companies;`

### Ошибка "Нет доступа"
1. Проверьте политики безопасности (RLS)
2. Убедитесь, что политики для чтения/записи созданы

---

## 🎯 ГОТОВО!

После настройки:
- ✅ Оба компьютера подключаются к одной базе
- ✅ Изменения видны на всех компьютерах
- ✅ Данные сохраняются в Supabase
- ✅ Работает в реальном времени

**Начинайте с выполнения SQL кода в Supabase!** 🚀
