#!/usr/bin/env bash
set -euo pipefail

ZSH_DIR="${ZSH:-$HOME/.oh-my-zsh}"
ZSH_CUSTOM_DIR="${ZSH_CUSTOM:-$ZSH_DIR/custom}"

if [[ ! -d "$ZSH_DIR" ]]; then
    echo "Oh My Zsh not found at $ZSH_DIR (expected common-utils feature)." >&2
    exit 1
fi

mkdir -p "$ZSH_CUSTOM_DIR/plugins" "$ZSH_CUSTOM_DIR/themes"

clone_or_update() {
    local repo="$1"
    local target="$2"
    if [[ -d "$target/.git" ]]; then
        git -C "$target" pull --ff-only
    elif [[ -e "$target" ]]; then
        echo "Target exists but is not a Git repository: $target" >&2
        exit 1
    else
        git clone --depth=1 "$repo" "$target"
    fi
}

clone_or_update https://github.com/zsh-users/zsh-autosuggestions.git "$ZSH_CUSTOM_DIR/plugins/zsh-autosuggestions"
clone_or_update https://github.com/zsh-users/zsh-syntax-highlighting.git "$ZSH_CUSTOM_DIR/plugins/zsh-syntax-highlighting"
clone_or_update https://github.com/frederik-hoeft/oh-my-zsh-themes.git "$HOME/.oh-my-zsh-themes"
ln -sf "$HOME/.oh-my-zsh-themes/themes/fred.zsh-theme" "$ZSH_CUSTOM_DIR/themes/fred.zsh-theme"
wget -q https://raw.githubusercontent.com/frederik-hoeft/dot-files/refs/heads/main/default.zshrc -O "$HOME/.zshrc"
wget -q https://raw.githubusercontent.com/frederik-hoeft/dot-files/refs/heads/main/default.vimrc -O "$HOME/.vimrc"
sed -i "s|/opt/shared/oh-my-zsh/oh-my-zsh|${ZSH_DIR}|g" "$HOME/.zshrc"
