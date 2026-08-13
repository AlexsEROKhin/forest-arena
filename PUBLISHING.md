# Публикация Forest Arena на GitHub Pages

GitHub Pages хранит статическую WebGL-версию игры. Unity Relay отвечает за соединение игроков и не запускается на GitHub.

## Обновление игры

1. В Unity выберите `Local PvP > Online > Build Web Version for GitHub Pages`.
2. Дождитесь сообщения `Web build is ready for GitHub Pages` в Console.
3. Сохраните изменения Git и отправьте их в GitHub.
4. Workflow `Publish Forest Arena to GitHub Pages` автоматически опубликует содержимое `Builds/WebGL`.

## Первое включение сайта

1. Репозиторий должен быть публичным, если используется бесплатный тариф GitHub.
2. Откройте на GitHub `Settings > Pages`.
3. В `Build and deployment > Source` выберите `GitHub Actions`.
4. Откройте вкладку `Actions` и дождитесь зелёной отметки у публикации.

Адрес будет иметь вид `https://ИМЯ-ПОЛЬЗОВАТЕЛЯ.github.io/ИМЯ-РЕПОЗИТОРИЯ/`.

## Онлайн-комнаты

Перед WebGL-сборкой свяжите проект с Unity Cloud через `Edit > Project Settings > Services`. Без этого сайт откроется, но создание и подключение к онлайн-комнатам через Relay работать не будет.
