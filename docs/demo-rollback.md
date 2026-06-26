# Откат учебного PR

Этот файл нужен для демонстрационного задания по GitHub.

После показа преподавателю изменения из `feature/ui` можно убрать одним revert-коммитом, потому что PR вливается через squash merge.

Команды для отката:

```powershell
git checkout main
git pull origin main
git revert HEAD
git push origin main
```

README на русском можно оставить, потому что он относится к первому заданию и полезен для проекта.
