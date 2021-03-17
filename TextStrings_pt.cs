using System.Windows;

namespace BetterHI3Launcher
{
    public partial class MainWindow : Window
    {
        private void TextStrings_Portuguese()
        {
            textStrings["version"] = "Versão";
            textStrings["launcher_version"] = "Versão do launcher";
            textStrings["outdated"] = "Desatualizado";
            textStrings["enabled"] = "Ativado";
            textStrings["disabled"] = "Desativado";
            textStrings["button_download"] = "Baixar";
            textStrings["button_downloading"] = "Baixando";
            textStrings["button_update"] = "Atualizar";
            textStrings["button_pause"] = "Pausar";
            textStrings["button_launch"] = "Iniciar";
            textStrings["button_options"] = "Opções";
            textStrings["button_resume"] = "Retomar";
            textStrings["button_confirm"] = "Confirmar";
            textStrings["button_cancel"] = "Cancelar";
            textStrings["button_github"] = "Ir para o repositório GitHub";
            textStrings["label_server"] = "Servidor";
            textStrings["label_mirror"] = "Espelho";
            textStrings["label_log"] = "Mostrar log";
            textStrings["contextmenu_downloadcache"] = "Baixar cache";
            textStrings["contextmenu_uninstall"] = "Desinstalar jogo";
            textStrings["contextmenu_fixsubs"] = "Corrigir legendas";
            textStrings["contextmenu_download_type"] = "Alterar tipo de download";
            textStrings["contextmenu_game_settings"] = "Gerenciar configurações de jogo";
            textStrings["contextmenu_customfps"] = "Definir limite de FPS personalizado";
            textStrings["contextmenu_customresolution"] = "Definir resolução personalizada";
            textStrings["contextmenu_resetgamesettings"] = "Redefinir as configurações do jogo";
            textStrings["contextmenu_web_profile"] = "Entrar no website";
            textStrings["contextmenu_feedback"] = "Enviar feedback";
            textStrings["contextmenu_changelog"] = "Mostrar changelog";
            textStrings["contextmenu_language"] = "Idioma";
            textStrings["contextmenu_language_system"] = "Padrão do sistema";
            textStrings["contextmenu_language_english"] = "Inglês";
            textStrings["contextmenu_language_russian"] = "Russo";
            textStrings["contextmenu_language_spanish"] = "Espanhol";
            textStrings["contextmenu_language_portuguese"] = "Português";
            textStrings["contextmenu_language_german"] = "Alemã";
            textStrings["contextmenu_language_vietnamese"] = "Vietnamita";
            textStrings["contextmenu_language_contribute"] = "Quer ajudar?";
            textStrings["contextmenu_about"] = "Sobre";
            textStrings["progresstext_error"] = "Erros foram encontrados :^(";
            textStrings["progresstext_verifying"] = "Verificando arquivos do jogo...";
            textStrings["progresstext_cleaningup"] = "Limpando...";
            textStrings["progresstext_checkingupdate"] = "Verificando atualizações...";
            textStrings["progresstext_downloadsize"] = "Tamanho do download";
            textStrings["progresstext_downloaded"] = "Baixado {0}/{1} ({2})";
            textStrings["progresstext_eta"] = "Tempo estimado: {0}";
            textStrings["progresstext_unpacking_1"] = "Extraindo arquivos do jogo...";
            textStrings["progresstext_unpacking_2"] = "Extraindo arquivo do jogo {0}/{1}...";
            textStrings["progresstext_uninstalling"] = "Desinstalando o jogo...";
            textStrings["progresstext_mirror_connect"] = "Conectando ao espelho...";
            textStrings["progresstext_initiating_download"] = "Iniciando download...";
            textStrings["progresstext_updating_launcher"] = "Atualizando launcher...";
            textStrings["introbox_title"] = "Bem-vindo ao Better Honkai Impact 3rd Launcher!";
            textStrings["introbox_msg_1"] = "!!! IMPORTANTE, POR FAVOR LEIA !!!";
            textStrings["introbox_msg_2"] = "Parece que esta é a primeira vezque usa este launcher. Em primeiro lugar, fico feliz que você tenha decidido dar uma chance, portanto, não hesite caso queira fornecer feedback.\nEm segundo lugar, é importante que se você usou o launcher oficial para atualizar o jogo e ainda não o iniciou (ao ponto de aindaestar na ponte), não use este launcher. Caso contrário, o launcher pode detectar a versão do jogo como antiga e fazer com que você tenha que baixá-la novamente.\n\nLeu tudo isso? Excelente! Se você já tem o jogo instalado, basta pressionar o botão de \"Baixar\" e selecionara pasta do jogo. O launcher irá detectar seu jogo e você não terá que baixá - lo novamente.";
            textStrings["downloadcachebox_msg"] = "Selecione se deseja baixar o pacote de cache completo ou apenas arquivos numéricos.\nSelecione\"Cache completo\" se você tiver problemas para atualizar os recursos do evento.\nSelecione \"Arquivos numéricos\" se você tiver problemas para atualizar as configurações.\nPor favor, note que atualmente não há como recuperar automaticamente o cache mais recente e temos que carregá-lo manualmente para um espelho.\nUsando espelho: {0}.\nCache atualizado por último: {1}.\nO mantenedor do espelho atual é {2}.";
            textStrings["downloadcachebox_button_full_cache"] = "Cache completo";
            textStrings["downloadcachebox_button_numeric_files"] = "Arquivos numéricos";
            textStrings["fpsinputbox_title"] = "Insira o limite de FPS customizado";
            textStrings["fpsinputbox_label_combatfps"] = "FPS em jogo";
            textStrings["fpsinputbox_label_menufps"] = "FPS no menu";
            textStrings["resolutioninputbox_title"] = "Insira a resolução customizada";
            textStrings["resolutioninputbox_label_width"] = "Largura";
            textStrings["resolutioninputbox_label_height"] = "Altura";
            textStrings["resolutioninputbox_label_fullscreen"] = "Tela cheia";
            textStrings["changelogbox_title"] = "Changelog";
            textStrings["changelogbox_msg"] = "Better Honkai Impact 3rd Launcher acaba de se tornar ainda melhor. Aqui está o que aconteceu:";
            textStrings["aboutbox_msg"] = "Bem, é muito mais avançado, não é? :^)\nEste projeto foi feito com a esperança de que muitos capitães tivessem uma melhor experiência com o jogo.\nEle não é afiliado à miHoYo e é totalmente código-aberto.\nQualquer comentário será muito bem vindo.\nAgradecimentos especiais à esses contribuidores do GitHub:\nSinsOfSeven - Contribuição de resolução personalizada\nProxy-E23 - Contribuição da idioma espanhola\nSpookyKisuy - Contribuição da idioma portuguêsa\nbulawin1 - Contribuição da idioma alemã\nKorewaLidesu - Contribuição da idioma vietnamita";
            textStrings["msgbox_genericerror_title"] = "Erro";
            textStrings["msgbox_genericerror_msg"] = "Ocorreu um erro.\nPara obter informações dê uma olhada no log.";
            textStrings["msgbox_neterror_title"] = "Erro de rede";
            textStrings["msgbox_neterror_msg"] = "Ocorreu um erro ao conectar ao servidor:\n{0}";
            textStrings["msgbox_verifyerror_title"] = "Erro de validação de arquivo";
            textStrings["msgbox_verifyerror_1_msg"] = "Ocorreu um erro durante o download. Por favor, tente novamente.";
            textStrings["msgbox_verifyerror_2_msg"] = "Ocorreu um erro durante o download. O arquivo pode estar corrompido.\nContinuar do mesmo jeito?";
            textStrings["msgbox_starterror_title"] = "Erro de inicialização";
            textStrings["msgbox_starterror_msg"] = "Ocorreu um erro ao iniciar o launcher:\n{0}";
            textStrings["msgbox_launcherdownloaderror_msg"] = "Ocorreu um erro ao baixar o launcher:\n{0}";
            textStrings["msgbox_gamedownloaderror_title"] = "Erro ao baixar arquivos de jogo";
            textStrings["msgbox_gamedownloaderror_msg"] = "Ocorreu um erro ao baixar os arquivos do jogo.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_installerror_msg"] = "Ocorreu um erro ao instalar os arquivos do jogo.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_installerror_title"] = "Erro de instalação";
            textStrings["msgbox_process_start_error_msg"] = "Ocorreu um erro ao iniciar o processo.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_update_title"] = "Aviso de atualização";
            textStrings["msgbox_install_msg"] = "O jogo será instalado em:\n{0}\nContinuar instalação?";
            textStrings["msgbox_install_title"] = "Aviso de instalação";
            textStrings["msgbox_installdirerror_msg"] = "Ocorreu um erro ao selecionar o diretório de instalação do jogo:\n{0}";
            textStrings["msgbox_installdirerror_title"] = "Diretório inválido";
            textStrings["msgbox_abort_1_msg"] = "Tem certeza de que deseja cancelar o download e fechar o launcher?";
            textStrings["msgbox_abort_2_msg"] = "O progresso não será salvo.";
            textStrings["msgbox_abort_3_msg"] = "O progresso será salvo.";
            textStrings["msgbox_abort_title"] = "Pedido de aborto";
            textStrings["msgbox_registryerror_msg"] = "Ocorreu um erro ao acessar o registro.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_registryerror_title"] = "Erro de registro";
            textStrings["msgbox_registryempty_1_msg"] = "Nenhum valor a ser ajustado no registro existe.";
            textStrings["msgbox_registryempty_2_msg"] = "Você já executou o jogo?";
            textStrings["msgbox_registryempty_3_msg"] = "Tente alterar as configurações de vídeo no jogo antes (desativando tela cheia, alterando a predefinição de vídeo, etc.).";
            textStrings["msgbox_download_cache_1_msg"] = "O cache completo está prestes a ser baixado.";
            textStrings["msgbox_download_cache_2_msg"] = "Os arquivos numéricos estão prestes a serem baixados.";
            textStrings["msgbox_download_cache_3_msg"] = "Tamanho do download: {0}.\nContinuar?";
            textStrings["msgbox_uninstall_1_msg"] = "Tem certeza que deseja desinstalar o jogo?";
            textStrings["msgbox_uninstall_2_msg"] = "Tem certeza absoluta que deseja desinstalar o jogo? :^(";
            textStrings["msgbox_uninstall_3_msg"] = "Remover as configurações e arquivos de cache do jogo também?";
            textStrings["msgbox_uninstall_4_msg"] = "Não é possível desinstalar o jogo enquanto o launcher está dentro do diretório do jogo. Mova o launcher para fora do diretório e tente novamente.";
            textStrings["msgbox_uninstall_title"] = "Desinstalar";
            textStrings["msgbox_uninstallerror_msg"] = "Ocorreu um erro ao desinstalar o jogo:\n{0}";
            textStrings["msgbox_uninstallerror_title"] = "Erro de desinstalação";
            textStrings["msgbox_download_type_1_msg"] = "Isso mudará o tipo de download de recursos do jogo em uma tentativa de consertar o infame loop de atualização que não permite que você entre no jogo.\nSe isso não resolver o problema, tente novamente.\nContinuar?";
            textStrings["msgbox_download_type_2_msg"] = "Valor ResourceDownloadType antes: {0}.\nValor ResourceDownloadType depois: {1}.";
            textStrings["msgbox_fixsubs_1_msg"] = "Isso tentará corrigir as legendas dos CGs (e banners de gacha). Certifique-se de que já baixou todos os CGs do jogo.\nContinuar?";
            textStrings["msgbox_fixsubs_2_msg"] = "Extraindo arquivo de legenda {0}/{1}...";
            textStrings["msgbox_fixsubs_3_msg"] = "Verificando arquivo de legenda {0}/{1}...";
            textStrings["msgbox_fixsubs_4_msg"] = "Legendas extraídas para {0} CGs.";
            textStrings["msgbox_fixsubs_5_msg"] = "Corrigido {0} arquivos de legenda";
            textStrings["msgbox_fixsubs_6_msg"] = "Nenhum arquivo de legenda foi corrigido. Eles ainda não foram baixados ou já estão corrigidos.";
            textStrings["msgbox_customfps_1_msg"] = "Os valores não devem estar vazios.";
            textStrings["msgbox_customfps_2_msg"] = "Os valores não devem serem menores que zero.";
            textStrings["msgbox_customfps_3_msg"] = "Valores inferiores a 30 não são recomendados. Continuar?";
            textStrings["msgbox_customfps_4_msg"] = "Limite de FPS em jogo e menu definidos para {0} e {1}, respectivamente.";
            textStrings["msgbox_customresolution_1_msg"] = "Altura maior que largura não é recomendado.\nContinuar?";
            textStrings["msgbox_customresolution_2_msg"] = "Resolução definida para {0}x{1} com tela cheia {2}.";
            textStrings["msgbox_resetgamesettings_1_msg"] = "Isso apagará todas as configurações do jogo armazenadas no registro.\nUse-o apenas se estiver tendo problemas com o jogo!\nContinuar?";
            textStrings["msgbox_resetgamesettings_2_msg"] = "Esta ação é irreversível. Você tem certeza de que quer fazer isso?";
            textStrings["msgbox_resetgamesettings_3_msg"] = "As configurações do jogo foram apagadas do registro.";
            textStrings["msgbox_extractskip_title"] = "Aviso de salto de arquivo";
            textStrings["msgbox_extractskip_msg"] = "A extração terminou, mas alguns arquivos não foram extraídos. Você pode querer extraí-los manualmente.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_noexe_title"] = "Nenhum jogo executável";
            textStrings["msgbox_noexe_msg"] = "O executável do jogo não foi encontrado :^(\nTente reinstalar o jogo.";
            textStrings["msgbox_installexisting_msg"] = "O jogo parece já ter sido instalado em:\n{0}\nUsar este diretório?";
            textStrings["msgbox_installexistinginvalid_msg"] = "O diretório selecionado não contém uma instalação válida do jogo. Este launcher só oferece suporte a clientes globais e SEA.";
            textStrings["msgbox_install_existing_no_local_version_msg"] = "Não foi possível determinar a versão local.\nSeu jogo já está atualizado? Por favor, escolha com sabedoria!\nSelecionar \"Sim\" fará com que você seja capaz de iniciar o jogo.\nSelecionar \"Não\" fará com que você tenha que baixar o jogo.";
            textStrings["msgbox_notice_title"] = "Aviso";
            textStrings["msgbox_novideodir_msg"] = "A pasta de vídeo não foi encontrada.\nTente reinstalar o jogo.";
            textStrings["msgbox_mirrorinfo_msg"] = "Use este espelho apenas se você não puder baixar o jogo através dos servidores miHoYo.\nNote que ele é atualizado manualmente.\nContinuar?";
            textStrings["msgbox_updatecheckerror_msg"] = "Ocorreu um erro ao verificar atualizações.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_updatecheckerror_title"] = "Erro de verificação de atualização";
            textStrings["msgbox_gamedownloadmirrorold_msg"] = "Parece que a versão do jogo nos servidores miHoYo é mais recente do que a do espelho.\nNão há motivo para baixar uma versão desatualizada, peça ao mantenedor do espelho para fazer o upload de uma nova versão.";
            textStrings["msgbox_gamedownloadpaused_msg"] = "O jogo ainda não foi totalmente baixado. Mudar de espelho ou servidor irá reiniciar o progresso do download.\nContinuar?";
            textStrings["msgbox_gamedownloadmirrorerror_msg"] = "Ocorreu um erro ao baixar do espelho.\nPara mais informações dê uma olhada no log.";
            textStrings["msgbox_install_little_space_msg"] = "Potencialmente, não há espaço livre suficiente no dispositivo selecionado, é recomendável liberar algum espaço ou a instalação pode resultar em falha.\nContinuar?";
            textStrings["msgbox_install_wrong_drive_type_msg"] = "Não é possível instalar no dispositivo selecionado.";
            textStrings["msgbox_mirror_error_msg"] = "Ocorreu um erro com o espelho. Peça ao mantenedor do espelho para chegar ao final disso.\nMensagem: {0}";
            textStrings["msgbox_net_version_old_msg"] = "Este launcher requer que o .NET Framework 4.6 ou mais recente esteja instalado.";
            textStrings["msgbox_language_msg"] = "O idioma será alterado para {0} e o launcher será reiniciado.\nContinuar?";
            textStrings["msgbox_no_internet_msg"] = "Não foi possível conectar-se à internet. Você está online?";
        }
    }
}